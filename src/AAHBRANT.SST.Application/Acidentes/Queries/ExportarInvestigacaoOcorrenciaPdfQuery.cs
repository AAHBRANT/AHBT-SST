using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Queries;

public record ExportarInvestigacaoOcorrenciaPdfQuery(Guid Id) : IRequest<byte[]>;

// Reaproveita ObterAcidenteDetalheQuery (mesmo escopo/permissão da tela) e só busca à parte o que o
// DTO não carrega: bytes das fotos e logo da obra — mesmo raciocínio de ExportarInspecaoPdfQuery.
public class ExportarInvestigacaoOcorrenciaPdfQueryHandler : IRequestHandler<ExportarInvestigacaoOcorrenciaPdfQuery, byte[]>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IInvestigacaoOcorrenciaPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;
    private readonly IImagemBiometricaCriptografia? _cripto;
    private readonly IQrCodeDocumentoService? _qrCode;

    public ExportarInvestigacaoOcorrenciaPdfQueryHandler(IMediator mediator, IAppDbContext db, IInvestigacaoOcorrenciaPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade,
        IImagemBiometricaCriptografia? cripto = null, IQrCodeDocumentoService? qrCode = null)
    {
        _cripto = cripto;
        _qrCode = qrCode;
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]> Handle(ExportarInvestigacaoOcorrenciaPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterAcidenteDetalheQuery(request.Id), ct);
        var a = detalhe.Acidente;

        var obraLogoConteudo = await _db.Obras
            .Where(o => o.Id == a.ObraId)
            .Select(o => o.LogoConteudo)
            .FirstOrDefaultAsync(ct);

        var fotos = (await _db.AcidentesFotos
                .Where(f => f.AcidenteId == request.Id)
                .OrderBy(f => f.Ordem)
                .Select(f => new { f.FotoConteudo, f.FotoMetadadosJson })
                .ToListAsync(ct))
            .Where(f => f.FotoConteudo.Length > 0)
            .Select(f =>
            {
                var dados = DadosCapturaFoto.Ler(f.FotoMetadadosJson);
                return new InvestigacaoOcorrenciaPdfFotoModelo(f.FotoConteudo, dados?.CapturadaEm?.UtcDateTime, dados?.Latitude, dados?.Longitude,
                    dados?.Origem == "arquivo", dados?.RecebidaEm?.UtcDateTime);
            })
            .ToList();

        // Envolvidos: lista nova (vários) ou, nos registros antigos, o trabalhador único.
        var envolvidos = a.Envolvidos.Count > 0
            ? a.Envolvidos.Select(e => e.Nome).ToList()
            : a.TrabalhadorNome is not null ? new List<string> { a.TrabalhadorNome } : new List<string>();

        var hoje = DateTime.UtcNow.Date;
        var acoes = detalhe.AcoesPlano.Select(acao =>
        {
            var concluida = acao.Status == StatusControleRisco.Concluido;
            // "Vencido" não existe no enum do banco — é derivado do prazo, igual à tela.
            var situacao = concluida ? "Concluída"
                : acao.Prazo is { } prazo && prazo.Date < hoje ? "Vencida"
                : acao.Status == StatusControleRisco.EmAndamento ? "Em andamento" : "Pendente";
            return new InvestigacaoOcorrenciaPdfAcaoModelo(
                acao.Descricao, acao.Fundamentacao, DescreverTipoAcao(acao.Tipo), acao.ResponsavelUsuarioNome, acao.Prazo, situacao, concluida);
        }).ToList();

        var reuniao = detalhe.Reuniao is { } r
            ? new InvestigacaoOcorrenciaPdfReuniaoModelo(r.Inicio, r.Fim, r.Participantes, r.Situacao == SituacaoReuniaoTeams.Criada,
                r.RealizadaEm, r.Presentes, r.Ata)
            : null;

        var rastreio = await _rastreabilidade.GarantirAsync(nameof(Acidente), request.Id, ct);

        // Assinatura eletrônica da ocorrência (faixa "Assinar ocorrência"). Desde 23/11/2026 são duas (técnico e
        // engenheiro): o documento em andamento também entra, para a seção 8 mostrar quem já assinou e quem falta;
        // o certificado de assinaturas continua só para o documento finalizado.
        var documentoAssinado = await _db.DocumentosAssinatura.AsNoTracking()
            .Include(d => d.Signatarios)
            .Where(d => d.EntidadeTipo == nameof(Acidente) && d.EntidadeId == request.Id
                && (d.Status == StatusDocumentoAssinatura.EmAndamento
                    || (d.Status == StatusDocumentoAssinatura.Finalizado && d.FinalizadoEm != null)))
            .OrderByDescending(d => d.Status == StatusDocumentoAssinatura.Finalizado)
            .ThenByDescending(d => d.FinalizadoEm)
            .FirstOrDefaultAsync(ct);
        // Só assinaturas ativas; nome/função carregados com IgnoreQueryFilters (CarregarSignatariosAsync),
        // para o signatário não sumir do PDF se o cadastro for excluído ou estiver fora do escopo de obra.
        var assinaturasAtivas = documentoAssinado?.Signatarios.Where(s => s.Ativo).OrderBy(s => s.AssinadoEm).ToList()
            ?? new List<DocumentoSignatario>();
        var dadosSignatarios = assinaturasAtivas.Count > 0
            ? await MontadorCertificadoAssinaturas.CarregarSignatariosAsync(_db, assinaturasAtivas.Select(s => s.TrabalhadorId), ct)
            : new Dictionary<Guid, MontadorCertificadoAssinaturas.DadosSignatario>();
        var signatarios = assinaturasAtivas.Select(s =>
        {
            dadosSignatarios.TryGetValue(s.TrabalhadorId, out var dados);
            return new InvestigacaoOcorrenciaPdfSignatarioModelo(dados?.Nome ?? "—", dados?.Funcao, s.AssinadoEm, s.MetodoAutenticacao);
        }).ToList();

        // 23/11/2026: Certificado de Assinaturas Eletrônicas no fim do PDF, igual a DDS/APR/PT/Inspeção.
        var tipoRotulo = TipoOcorrenciaRotulo.Descrever(a.Tipo);
        var painel = new PainelCertificado("Ocorrência", $"{tipoRotulo} · {a.Data:dd/MM/yyyy}", new List<SecaoPainelCertificado>
        {
            new("Ocorrência", new List<LinhaPainelCertificado>
            {
                new("Tipo", tipoRotulo),
                new("Data", a.Data.ToString("dd/MM/yyyy")),
                new("Local", string.IsNullOrWhiteSpace(a.Local) ? "—" : a.Local),
                new("Situação", DescreverStatus(a.Status)),
            }),
        });
        var certificado = documentoAssinado is null || assinaturasAtivas.Count == 0
                || documentoAssinado.Status != StatusDocumentoAssinatura.Finalizado
            ? null
            : await MontadorCertificadoAssinaturas.MontarAsync(
                _db, _cripto, _qrCode, documentoAssinado, assinaturasAtivas, dadosSignatarios,
                _ => "Investigação de ocorrência",
                "Investigação de ocorrência",
                $"Investigação de ocorrência — {tipoRotulo} · {a.Data:dd/MM/yyyy} · {a.ObraNome}",
                a.ObraNome ?? string.Empty, obraLogoConteudo, rastreio, painel, compacto: false, ct);

        var modelo = new InvestigacaoOcorrenciaPdfModelo(
            a.ObraNome,
            obraLogoConteudo,
            TipoOcorrenciaRotulo.Descrever(a.Tipo),
            a.Data,
            a.Hora,
            a.Local,
            a.AtividadeNome,
            envolvidos,
            DescreverStatus(a.Status),
            a.DataConclusaoInvestigacao,
            a.Descricao,
            DescreverGravidade(a.Gravidade),
            a.HouveAfastamento,
            a.DiasAfastamento,
            a.NumeroCat,
            a.DiasDebitados,
            a.Lesao,
            a.Atendimento,
            a.Consequencia,
            a.MetodologiaInvestigacao is { } m ? DescreverMetodologia(m) : null,
            a.Causas,
            acoes,
            reuniao,
            fotos,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng,
            rastreio.TemAssinatura,
            a.RegioesCorpo,
            a.Testemunhas.Select(t => t.Nome).ToList(),
            a.PessoasExternas.Select(p => new InvestigacaoOcorrenciaPdfPessoaExternaModelo(
                p.Nome, DescreverVinculo(p.Vinculo), p.Empresa, p.CpfMascarado, p.Idade,
                DescreverResultado(p.Resultado), p.Lesao, p.Atendimento)).ToList(),
            a.ConclusaoInvestigacao,
            signatarios,
            documentoAssinado?.ConteudoHash,
            certificado);

        var pdf = _pdf.Gerar(modelo);
        await _rastreabilidade.RegistrarArquivoAsync(rastreio.DocumentoId, pdf, ct);
        return pdf;
    }

    // Mesmos rótulos da tela (lib/api.ts: statusAcidenteLabel etc.).
    private static string DescreverStatus(StatusAcidente status) => status switch
    {
        StatusAcidente.Registrado => "Registrada",
        StatusAcidente.EmInvestigacao => "Em investigação",
        StatusAcidente.Concluido => "Investigação concluída",
        StatusAcidente.AguardandoLaudo => "Aguardando laudo",
        _ => status.ToString(),
    };

    private static string DescreverVinculo(VinculoPessoaExterna vinculo) => vinculo switch
    {
        VinculoPessoaExterna.Terceiro => "Terceiro",
        VinculoPessoaExterna.Visitante => "Visitante",
        VinculoPessoaExterna.Transeunte => "Transeunte",
        VinculoPessoaExterna.Outro => "Outro",
        _ => vinculo.ToString(),
    };

    private static string DescreverResultado(ResultadoPessoaExterna resultado) => resultado switch
    {
        ResultadoPessoaExterna.SemLesao => "Sem lesão",
        ResultadoPessoaExterna.Lesao => "Com lesão",
        ResultadoPessoaExterna.Obito => "Óbito",
        _ => resultado.ToString(),
    };

    private static string DescreverGravidade(GravidadeAcidente gravidade) => gravidade switch
    {
        GravidadeAcidente.SemAfastamento => "Sem afastamento",
        GravidadeAcidente.ComAfastamento => "Com afastamento",
        GravidadeAcidente.IncapacidadePermanenteParcial => "Incapacidade permanente parcial",
        GravidadeAcidente.IncapacidadePermanenteTotal => "Incapacidade permanente total",
        GravidadeAcidente.Obito => "Óbito",
        _ => gravidade.ToString(),
    };

    private static string DescreverMetodologia(MetodologiaInvestigacao metodologia) => metodologia switch
    {
        MetodologiaInvestigacao.AnaliseCausaRaiz => "Análise de causa raiz",
        MetodologiaInvestigacao.CincoPorques => "5 Porquês",
        MetodologiaInvestigacao.ArvoreDeCausas => "Árvore de causas",
        MetodologiaInvestigacao.FatoresContribuintes => "Fatores contribuintes",
        MetodologiaInvestigacao.FalhasDeBarreira => "Falhas de barreira",
        _ => metodologia.ToString(),
    };

    private static string DescreverTipoAcao(TipoAcaoPlano tipo) => tipo switch
    {
        TipoAcaoPlano.Corretiva => "Corretiva",
        TipoAcaoPlano.Preventiva => "Preventiva",
        TipoAcaoPlano.Melhoria => "Melhoria",
        _ => tipo.ToString(),
    };
}
