using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Queries;

public record ExportarFichaEpiTrabalhadorQuery(Guid TrabalhadorId) : IRequest<byte[]?>;

public class ExportarFichaEpiTrabalhadorQueryHandler : IRequestHandler<ExportarFichaEpiTrabalhadorQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly IFichaEpiPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;
    private readonly IImagemBiometricaCriptografia? _imagemCriptografia;

    public ExportarFichaEpiTrabalhadorQueryHandler(
        IAppDbContext db, IFichaEpiPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade,
        IImagemBiometricaCriptografia? imagemCriptografia = null)
    {
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
        _imagemCriptografia = imagemCriptografia;
    }

    public async Task<byte[]?> Handle(ExportarFichaEpiTrabalhadorQuery request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores
            .Include(t => t.Obra)
            .Include(t => t.Funcao)
            .FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct);
        if (trabalhador is null) return null;

        // Ficha NR-6 tem valor de prova em fiscalização — uma entrega apenas reservada
        // (Confirmada=false, automação de EPI do módulo Terceirizado) nunca deve aparecer como se
        // tivesse sido fisicamente entregue. Decisão do usuário (revisão final do módulo
        // Terceirizado, 2026-09-18): excluir da ficha em vez de marcar como "reservada".
        var entregas = await _db.EntregasEpi
            .Include(e => e.CatalogoEpi)
            .Where(e => e.TrabalhadorId == request.TrabalhadorId && e.Confirmada)
            .OrderBy(e => e.DataEntrega)
            .ToListAsync(ct);

        var entregaIds = entregas.Select(e => e.Id).ToList();

        var termo = await TermosCompromissoEpi.TermoCompromissoEpiConsulta.ObterAsync(_db, request.TrabalhadorId, ct);

        // Mesma regra da tela de entrega (EntregasTab): entre os certificados de NR-06 do
        // trabalhador vale o de validade mais distante, para um certificado antigo lançado depois
        // do novo não tomar o lugar do válido. Alimenta a cláusula 2 do termo de compromisso.
        var certificadoNr6 = await _db.Treinamentos.AsNoTracking()
            .Where(t => t.TrabalhadorId == request.TrabalhadorId && t.CursoTreinamento!.AtendeNr6)
            .OrderByDescending(t => t.DataValidade)
            .Select(t => new { t.DataRealizacao, t.NumeroCertificado })
            .FirstOrDefaultAsync(ct);

        // Um DocumentoAssinatura por entrega/devolução (EntidadeTipo="EntregaEpi"/"DevolucaoEpi",
        // EntidadeId=EntregaEpi.Id) — ver docs/Motor-Assinatura-Eletronica.md. Carrega tudo de uma vez
        // e agrupa em memória em vez de uma query por linha da ficha.
        var documentos = await _db.DocumentosAssinatura
            .Include(d => d.Signatarios)
            .Where(d => entregaIds.Contains(d.EntidadeId) && (d.EntidadeTipo == "EntregaEpi" || d.EntidadeTipo == "DevolucaoEpi"))
            .ToListAsync(ct);

        // GroupBy em vez de ToDictionary: dados de produção anteriores ao fix de idempotência do
        // CriarDocumentoAssinaturaCommand podem ter mais de um DocumentoAssinatura para a mesma
        // (EntidadeTipo, EntidadeId) — nesse caso fica com o mais "completo" (Finalizado antes de
        // EmAndamento) e, empatando, o mais recente, em vez de derrubar a ficha inteira com exceção.
        var documentosEntrega = documentos
            .Where(d => d.EntidadeTipo == "EntregaEpi")
            .GroupBy(d => d.EntidadeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.Status == StatusDocumentoAssinatura.Finalizado)
                .ThenByDescending(d => d.CreatedAtUtc).First());
        var documentosDevolucao = documentos
            .Where(d => d.EntidadeTipo == "DevolucaoEpi")
            .GroupBy(d => d.EntidadeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.Status == StatusDocumentoAssinatura.Finalizado)
                .ThenByDescending(d => d.CreatedAtUtc).First());

        var linhasEntrega = new List<LinhaEntregaEpiPdf>();
        var linhasDevolucao = new List<LinhaDevolucaoEpiPdf>();
        var numero = 0;

        foreach (var entrega in entregas)
        {
            numero++;

            var assinaturaEmpregado = documentosEntrega.TryGetValue(entrega.Id, out var docEntrega)
                ? docEntrega.Signatarios
                    .Where(s => s.TrabalhadorId == entrega.TrabalhadorId)
                    .OrderBy(s => s.AssinadoEm)
                    .FirstOrDefault()
                : null;
            var assinaturaResponsavel = docEntrega?.Signatarios
                .Where(s => s.MetodoAutenticacao == MetodoAutenticacaoAssinatura.SessaoLogada)
                .OrderBy(s => s.AssinadoEm)
                .FirstOrDefault();

            linhasEntrega.Add(new LinhaEntregaEpiPdf(
                numero,
                entrega.CatalogoEpi?.Nome ?? string.Empty,
                entrega.CatalogoEpi?.CertificadoAprovacaoNumero,
                entrega.MotivoTipo,
                entrega.Motivo,
                entrega.Quantidade,
                entrega.DataEntrega,
                assinaturaEmpregado is not null,
                assinaturaResponsavel is not null,
                assinaturaEmpregado?.AssinadoEm,
                assinaturaResponsavel?.AssinadoEm,
                assinaturaEmpregado?.MetodoAutenticacao,
                assinaturaResponsavel?.MetodoAutenticacao));

            if (entrega.DataDevolucao is null) continue;

            var assinaturaDevolucaoEmpregado = documentosDevolucao.TryGetValue(entrega.Id, out var docDevolucao)
                ? docDevolucao.Signatarios
                    .Where(s => s.TrabalhadorId == entrega.TrabalhadorId)
                    .OrderBy(s => s.AssinadoEm)
                    .FirstOrDefault()
                : null;

            linhasDevolucao.Add(new LinhaDevolucaoEpiPdf(
                numero,
                entrega.CatalogoEpi?.Nome ?? string.Empty,
                entrega.QuantidadeDevolucao ?? entrega.Quantidade,
                entrega.DataDevolucao.Value,
                assinaturaDevolucaoEmpregado is not null,
                assinaturaDevolucaoEmpregado?.AssinadoEm,
                entrega.VistoConsorcioResponsavel,
                assinaturaDevolucaoEmpregado?.MetodoAutenticacao));
        }

        // Chave sintética "FichaEpiTrabalhador"/TrabalhadorId: a Ficha agrega N entregas, cada uma já
        // individualmente rastreável (DocumentoAssinatura por entrega, acima) — ninguém assina a Ficha
        // em si, esta rastreabilidade é só pra atestar integridade do PDF impresso como um todo.
        var rastreio = await _rastreabilidade.GarantirAsync("FichaEpiTrabalhador", request.TrabalhadorId, ct);

        var log = await MontarLogAsync(trabalhador, entregas, documentos, rastreio.DocumentoId, ct);

        var modelo = new FichaEpiPdfModelo(
            trabalhador.Obra?.Nome ?? string.Empty,
            trabalhador.Obra?.Cliente,
            trabalhador.Obra?.Cnpj,
            trabalhador.Obra?.LogoConteudo,
            trabalhador.Obra?.LogoContentType,
            trabalhador.Nome,
            CpfMascarador.Mascarar(trabalhador.Cpf),
            trabalhador.Matricula ?? string.Empty,
            trabalhador.Funcao?.Nome ?? string.Empty,
            trabalhador.Turno,
            trabalhador.DataAdmissao,
            linhasEntrega,
            linhasDevolucao,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng,
            certificadoNr6?.DataRealizacao,
            certificadoNr6?.NumeroCertificado,
            termo,
            CpfMascarador.Formatar(trabalhador.Cpf),
            trabalhador.FotoConteudo,
            log);

        var pdf = _pdf.Gerar(modelo);
        // Guarda a cópia exata emitida e o SHA-256 dela — é o que permite conferir, depois,
        // que o arquivo em mãos não foi adulterado (ver HashArquivoCalculador).
        await _rastreabilidade.RegistrarArquivoAsync(rastreio.DocumentoId, pdf, ct);
        return pdf;
    }

    // Janela para juntar as assinaturas de um mesmo carrinho: no lote, cada documento é assinado numa
    // chamada própria, com poucos segundos entre elas, e o cupom é um só.
    private static readonly TimeSpan JanelaMesmoCarrinho = TimeSpan.FromSeconds(20);

    private async Task<LogAssinaturasFichaEpi?> MontarLogAsync(
        Domain.Entidades.Trabalhador trabalhador,
        List<Domain.Entidades.EntregaEpi> entregas,
        List<Domain.Entidades.DocumentoAssinatura> documentos,
        Guid registroId,
        CancellationToken ct)
    {
        var documentoTermo = await _db.DocumentosAssinatura.AsNoTracking()
            .Include(d => d.Signatarios)
            .Where(d => d.EntidadeTipo == "TermoCompromissoEpi" && d.EntidadeId == trabalhador.Id)
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        // Assinaturas do próprio funcionário em cada documento de EPI.
        var entregasPorId = entregas.ToDictionary(e => e.Id);
        var assinaturas = new List<AssinaturaDoLog>();
        foreach (var doc in documentos)
        {
            var sig = doc.Signatarios.Where(s => s.TrabalhadorId == trabalhador.Id).OrderBy(s => s.AssinadoEm).FirstOrDefault();
            if (sig is null) continue;
            entregasPorId.TryGetValue(doc.EntidadeId, out var entrega);
            assinaturas.Add(new AssinaturaDoLog(doc, sig, entrega));
        }
        if (documentoTermo is not null)
        {
            var sigTermo = documentoTermo.Signatarios.Where(s => s.TrabalhadorId == trabalhador.Id).OrderBy(s => s.AssinadoEm).FirstOrDefault();
            if (sigTermo is not null) assinaturas.Add(new AssinaturaDoLog(documentoTermo, sigTermo, null));
        }
        if (assinaturas.Count == 0) return null;

        // Cadastros biométricos com histórico (inclui arquivados): vale o vigente na data de cada assinatura.
        var templates = await _db.TemplatesBiometricoFutronic.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.TrabalhadorId == trabalhador.Id)
            .OrderBy(t => t.CapturadoEm)
            .Select(t => new { t.CapturadoEm, t.ImagemCadastroCriptografada })
            .ToListAsync(ct);
        var fotosFace = await _db.FotosCadastroFacial.AsNoTracking().IgnoreQueryFilters()
            .Where(f => f.TrabalhadorId == trabalhador.Id)
            .OrderBy(f => f.CapturadaEm)
            .Select(f => new { f.Conteudo, f.CapturadaEm, f.HashSha256 })
            .ToListAsync(ct);

        LogAssinaturaEpiItem Montar(TipoLogAssinaturaEpi tipo, string descricao, AssinaturaDoLog a, CupomEntregaEpi? cupom)
        {
            var sig = a.Sig;
            byte[]? fotoCadastro = null; DateTime? fotoCadastroEm = null; string? fotoCadastroHash = null;
            byte[]? digitalImagem = null; DateTime? digitalEm = null;
            if (sig.MetodoAutenticacao == MetodoAutenticacaoAssinatura.ReconhecimentoFacial)
            {
                var foto = fotosFace.LastOrDefault(f => f.CapturadaEm <= sig.AssinadoEm);
                if (foto is not null) { fotoCadastro = foto.Conteudo; fotoCadastroEm = foto.CapturadaEm; fotoCadastroHash = foto.HashSha256; }
            }
            else if (sig.MetodoAutenticacao == MetodoAutenticacaoAssinatura.Biometria)
            {
                var template = templates.LastOrDefault(t => t.CapturadoEm <= sig.AssinadoEm);
                if (template is not null)
                {
                    digitalEm = template.CapturadoEm;
                    if (!string.IsNullOrEmpty(template.ImagemCadastroCriptografada) && _imagemCriptografia is not null)
                    {
                        try { digitalImagem = _imagemCriptografia.Descriptografar(template.ImagemCadastroCriptografada); }
                        catch { digitalImagem = null; }
                    }
                }
            }

            return new LogAssinaturaEpiItem(
                tipo, descricao, sig.MetodoAutenticacao, sig.AssinadoEm, a.Doc.CreatedAtUtc,
                sig.IpAddress, sig.UserAgent, sig.LocalizacaoStatus, sig.Latitude, sig.Longitude, sig.PrecisaoMetros,
                sig.ValidacaoModelo, sig.ValidacaoGrupoId, sig.ValidacaoRequisicaoId, sig.DispositivoAgenteId,
                sig.FotoEvidenciaConteudo, sig.FotoEvidenciaHash,
                fotoCadastro, fotoCadastroEm, fotoCadastroHash, digitalImagem, digitalEm, cupom);
        }

        var itens = new List<LogAssinaturaEpiItem>();

        // Entregas: o carrinho assinado de uma vez vira um só item, com o cupom de todos os EPIs dele.
        var entregasAssinadas = assinaturas
            .Where(a => a.Entrega is not null && a.Doc.EntidadeTipo == "EntregaEpi")
            .OrderBy(a => a.Sig.AssinadoEm)
            .ToList();
        var grupos = new List<List<AssinaturaDoLog>>();
        foreach (var a in entregasAssinadas)
        {
            var atual = grupos.LastOrDefault();
            if (atual is not null
                && atual[^1].Sig.MetodoAutenticacao == a.Sig.MetodoAutenticacao
                && a.Sig.AssinadoEm - atual[^1].Sig.AssinadoEm <= JanelaMesmoCarrinho)
                atual.Add(a);
            else
                grupos.Add(new List<AssinaturaDoLog> { a });
        }
        foreach (var grupo in grupos)
        {
            var primeira = grupo[0];
            var e0 = primeira.Entrega!;
            var cupom = new CupomEntregaEpi(
                e0.DataEntrega,
                DescreverMotivo(e0.MotivoTipo) ?? e0.Motivo,
                e0.MotivoTipo is null ? null : e0.Motivo,
                e0.NumeroListaPresencaNr6,
                e0.DataTreinamentoNr6,
                e0.VistoConsorcioResponsavel,
                grupo.Select(g => new ItemCupomEpi(
                    g.Entrega!.CatalogoEpi?.Nome ?? string.Empty,
                    g.Entrega.CatalogoEpi?.Fabricante,
                    g.Entrega.Quantidade,
                    g.Entrega.CatalogoEpi?.CertificadoAprovacaoNumero,
                    g.Entrega.CatalogoEpi?.CertificadoAprovacaoValidade,
                    g.Entrega.DataValidade,
                    g.Entrega.CatalogoEpi?.FotoConteudo)).ToList());
            itens.Add(Montar(TipoLogAssinaturaEpi.Entrega,
                $"Entrega de EPI — {grupo.Count} {(grupo.Count == 1 ? "item" : "itens")}", primeira, cupom));
        }

        foreach (var a in assinaturas.Where(a => a.Doc.EntidadeTipo == "DevolucaoEpi" && a.Entrega is not null))
            itens.Add(Montar(TipoLogAssinaturaEpi.Devolucao,
                $"Devolução de EPI — {a.Entrega!.CatalogoEpi?.Nome} (qtd {a.Entrega.QuantidadeDevolucao ?? a.Entrega.Quantidade})", a, null));

        foreach (var a in assinaturas.Where(a => a.Doc.EntidadeTipo == "TermoCompromissoEpi"))
            itens.Add(Montar(TipoLogAssinaturaEpi.TermoCompromisso, "Termo de recebimento e compromisso de uso", a, null));

        itens = itens.OrderBy(i => i.AssinadoEmUtc).ToList();
        return new LogAssinaturasFichaEpi(
            registroId.ToString(), DateTime.UtcNow, itens.Count,
            trabalhador.TermoAceiteAssinaturaEletronicaEm, trabalhador.ConsentimentoBiometriaEm, itens);
    }

    private sealed record AssinaturaDoLog(
        Domain.Entidades.DocumentoAssinatura Doc, Domain.Entidades.DocumentoSignatario Sig, Domain.Entidades.EntregaEpi? Entrega);

    private static string? DescreverMotivo(MotivoEntregaEpi? motivo) => motivo switch
    {
        MotivoEntregaEpi.Inicial => "Entrega inicial",
        MotivoEntregaEpi.Dano => "Dano",
        MotivoEntregaEpi.Extravio => "Extravio",
        MotivoEntregaEpi.Vencimento => "Vencimento",
        MotivoEntregaEpi.TrocaDeFuncao => "Troca de função",
        _ => null,
    };
}
