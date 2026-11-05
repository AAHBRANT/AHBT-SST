using AAHBRANT.SST.Application.Acidentes.Queries;
using AAHBRANT.SST.Application.Aprs.Queries;
using AAHBRANT.SST.Application.Asos.Commands;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.Dds;
using AAHBRANT.SST.Application.Dds.Queries;
using AAHBRANT.SST.Application.EntregasEpi.Queries;
using AAHBRANT.SST.Application.Inspecoes.Queries;
using AAHBRANT.SST.Application.PermissoesTrabalho.Queries;
using AAHBRANT.SST.Application.SessoesTreinamento.Queries;
using AAHBRANT.SST.Application.TermosCompromissoEpi.Queries;
using AAHBRANT.SST.Application.Treinamentos.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

// Dossiê do Funcionário (29/10): um PDF só, com o Relatório de Fiscalização na frente, um índice e,
// em seguida, cada documento do funcionário aberto na íntegra — certificados, listas de presença,
// DDS, Ficha de EPI (com o log de assinaturas e as evidências de cada entrega), ASO e os demais
// documentos que ele assinou. Substitui o botão "Emitir relatório de fiscalização" do perfil.
//
// Cada documento é gerado pela mesma consulta do botão de exportar dele, então respeita o escopo de
// obra de quem baixa: o que não puder ser visto ou gerado fica fora do PDF e aparece no índice como
// "não incluído", com o motivo — o dossiê nunca falha inteiro por causa de um documento.
public record GerarDossieTrabalhadorQuery(Guid Id) : IRequest<byte[]?>;

public class GerarDossieTrabalhadorQueryHandler : IRequestHandler<GerarDossieTrabalhadorQuery, byte[]?>
{
    private const string SecaoCertificados = "Certificados de treinamento";
    private const string SecaoTurmas = "Listas de presença de treinamento";
    private const string SecaoDds = "DDS — Diálogo Diário de Segurança";
    private const string SecaoEpi = "EPI";
    private const string SecaoAso = "ASO — Atestado de Saúde Ocupacional";
    private const string SecaoOutros = "Outros documentos assinados";

    // Tipos que já entram por outro caminho: o certificado vem da lista de treinamentos do funcionário
    // e as assinaturas de EPI estão todas dentro da Ficha de EPI (log de assinaturas + cupons).
    private static readonly HashSet<string> TiposCobertosPorOutraSecao = new(StringComparer.OrdinalIgnoreCase)
    {
        "Treinamento", "EntregaEpi", "DevolucaoEpi", "TermoCompromissoEpi",
    };

    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IPdfMesclador _mesclador;
    private readonly IDossieTrabalhadorPdfService _pdf;

    public GerarDossieTrabalhadorQueryHandler(IAppDbContext db, IMediator mediator, IPdfMesclador mesclador, IDossieTrabalhadorPdfService pdf)
    {
        _db = db;
        _mediator = mediator;
        _mesclador = mesclador;
        _pdf = pdf;
    }

    public async Task<byte[]?> Handle(GerarDossieTrabalhadorQuery request, CancellationToken ct)
    {
        // Mesmo portão do relatório: fora do escopo de obra, o dossiê não existe para quem pediu.
        var relatorio = await _mediator.Send(new GerarRelatorioFiscalizacaoTrabalhadorQuery(request.Id), ct);
        if (relatorio is null) return null;

        var trabalhador = await _db.Trabalhadores.AsNoTracking()
            .Where(t => t.Id == request.Id)
            .Select(t => new { t.Nome, ObraNome = t.Obra!.Nome, ObraLogo = t.Obra!.LogoConteudo })
            .FirstAsync(ct);

        var documentos = new List<(ItemIndiceDossie Item, byte[]? Pdf)>();
        void Adicionar(string secao, string titulo, DateTime? data, byte[]? pdf, string motivoAusencia)
            => documentos.Add((new ItemIndiceDossie(secao, titulo, data, pdf is not null, pdf is null ? motivoAusencia : null), pdf));

        await AdicionarCertificadosAsync(request.Id, trabalhador.ObraNome, trabalhador.ObraLogo, Adicionar, ct);
        await AdicionarDocumentosAssinadosAsync(request.Id, Adicionar, ct);
        await AdicionarEpiAsync(request.Id, trabalhador.ObraNome, trabalhador.ObraLogo, Adicionar, ct);
        await AdicionarAsosAsync(request.Id, trabalhador.ObraNome, trabalhador.ObraLogo, Adicionar, ct);

        var ordemSecao = new[] { SecaoCertificados, SecaoTurmas, SecaoDds, SecaoEpi, SecaoAso, SecaoOutros };
        documentos = documentos
            .OrderBy(d => Array.IndexOf(ordemSecao, d.Item.Secao))
            .ThenBy(d => d.Item.Data ?? DateTime.MaxValue)
            .ToList();

        byte[] Montar() => _mesclador.Mesclar(
            new[] { relatorio, _pdf.GerarIndice(trabalhador.Nome, trabalhador.ObraNome, trabalhador.ObraLogo, documentos.Select(d => d.Item).ToList()) }
                .Concat(documentos.Where(d => d.Pdf is not null).Select(d => d.Pdf!))
                .ToList());

        try
        {
            return Montar();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Algum anexo que o qpdf não consegue ler (arquivo danificado). Descobre qual testando um a
            // um, tira do PDF, marca no índice e monta de novo — um anexo ruim não derruba o dossiê.
            for (var i = 0; i < documentos.Count; i++)
            {
                if (documentos[i].Pdf is not { } pdf) continue;
                try
                {
                    _mesclador.Mesclar(new[] { pdf, pdf });
                }
                catch (Exception exAnexo) when (exAnexo is not OperationCanceledException)
                {
                    documentos[i] = (documentos[i].Item with { Incluido = false, Observacao = "Arquivo danificado: não pôde ser juntado ao dossiê." }, null);
                }
            }
            return Montar();
        }
    }

    private delegate void AdicionarDocumento(string secao, string titulo, DateTime? data, byte[]? pdf, string motivoAusencia);

    private async Task AdicionarCertificadosAsync(Guid trabalhadorId, string obraNome, byte[]? obraLogo, AdicionarDocumento adicionar, CancellationToken ct)
    {
        var treinamentos = await _db.Treinamentos.AsNoTracking()
            .NoEscopoDoTrabalhador(_db, t => t.TrabalhadorId)
            .Where(t => t.TrabalhadorId == trabalhadorId)
            .Select(t => new
            {
                t.Id,
                Curso = t.CursoTreinamento!.Nome,
                t.NumeroCertificado,
                t.DataRealizacao,
                TemArquivo = _db.ArquivosCertificadoTreinamento.Any(a => a.TreinamentoId == t.Id),
            })
            .ToListAsync(ct);

        foreach (var t in treinamentos)
        {
            // Número no índice para o fiscal achar o certificado certo (ex.: "Certificado nº 0123 — NR-35").
            var titulo = string.IsNullOrWhiteSpace(t.NumeroCertificado)
                ? $"Certificado — {t.Curso}"
                : $"Certificado nº {t.NumeroCertificado} — {t.Curso}";
            byte[]? pdf = null;
            var motivo = "Não foi possível gerar o certificado.";
            try
            {
                // Certificado retroativo (digitalizado) tem precedência: é o documento original.
                if (t.TemArquivo)
                {
                    var arquivo = await _mediator.Send(new ObterArquivoCertificadoTreinamentoQuery(t.Id), ct);
                    if (arquivo is not null)
                        (pdf, motivo) = ArquivoComoPdf(titulo, arquivo.Conteudo, arquivo.NomeArquivo, obraNome, obraLogo);
                }
                pdf ??= await _mediator.Send(new ExportarCertificadoTreinamentoQuery(t.Id), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                pdf = null;
            }
            adicionar(SecaoCertificados, titulo, t.DataRealizacao, pdf, motivo);
        }
    }

    private async Task AdicionarDocumentosAssinadosAsync(Guid trabalhadorId, AdicionarDocumento adicionar, CancellationToken ct)
    {
        // Um documento por linha, mesmo que o funcionário tenha assinado mais de uma vez (ex.: papéis
        // diferentes na mesma PT): vale a primeira assinatura dele como data.
        var assinados = await _db.DocumentoSignatarios.AsNoTracking()
            .Where(s => s.TrabalhadorId == trabalhadorId)
            .GroupBy(s => new { s.DocumentoAssinaturaId, s.DocumentoAssinatura!.EntidadeTipo, s.DocumentoAssinatura.EntidadeId })
            .Select(g => new { g.Key.DocumentoAssinaturaId, g.Key.EntidadeTipo, g.Key.EntidadeId, AssinadoEm = g.Min(s => s.AssinadoEm) })
            .ToListAsync(ct);

        foreach (var doc in assinados.Where(d => !TiposCobertosPorOutraSecao.Contains(d.EntidadeTipo)))
        {
            var secao = doc.EntidadeTipo switch
            {
                "Dds" or "DdsSemanal" => SecaoDds,
                "SessaoTreinamento" => SecaoTurmas,
                _ => SecaoOutros,
            };
            byte[]? pdf;
            try
            {
                pdf = doc.EntidadeTipo switch
                {
                    "Dds" => await _mediator.Send(new ExportarDdsPdfQuery(doc.EntidadeId), ct),
                    "DdsSemanal" => await _mediator.Send(new ExportarDdsSemanalPdfQuery(doc.EntidadeId), ct),
                    "SessaoTreinamento" => await _mediator.Send(new ExportarAtaSessaoTreinamentoQuery(doc.EntidadeId), ct),
                    "Inspecao" => await _mediator.Send(new ExportarInspecaoPdfQuery(doc.EntidadeId), ct),
                    "PermissaoTrabalho" => await _mediator.Send(new ExportarPermissaoTrabalhoPdfQuery(doc.EntidadeId), ct),
                    "Apr" => await _mediator.Send(new ExportarAprPdfQuery(doc.EntidadeId), ct),
                    "Acidente" => await _mediator.Send(new ExportarInvestigacaoOcorrenciaPdfQuery(doc.EntidadeId), ct),
                    // Sem exportador próprio (Entrega de Uniforme, PGR, NC...): o PDF guardado no
                    // registro de assinatura, se o escopo permitir.
                    _ => await _mediator.Send(new ObterPdfDocumentoQuery(doc.DocumentoAssinaturaId), ct),
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                pdf = null;
            }

            adicionar(secao, TipoDocumentoAssinatura.Rotulo(doc.EntidadeTipo), doc.AssinadoEm, pdf,
                "Documento fora do escopo de obra de quem emitiu o dossiê, ou sem arquivo gerado.");
        }
    }

    private async Task AdicionarEpiAsync(Guid trabalhadorId, string obraNome, byte[]? obraLogo, AdicionarDocumento adicionar, CancellationToken ct)
    {
        var temEntrega = await _db.EntregasEpi.AnyAsync(e => e.TrabalhadorId == trabalhadorId, ct);
        if (temEntrega)
        {
            byte[]? ficha;
            try { ficha = await _mediator.Send(new ExportarFichaEpiTrabalhadorQuery(trabalhadorId), ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { ficha = null; }
            adicionar(SecaoEpi, "Ficha de EPI (entregas, devoluções e log de assinaturas)", null, ficha, "Não foi possível gerar a Ficha de EPI.");
        }

        ArquivoTermoManualEpiDto? termo;
        try { termo = await _mediator.Send(new ObterArquivoTermoManualEpiQuery(trabalhadorId), ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { termo = null; }
        if (termo is not null)
        {
            const string titulo = "Termo de compromisso de EPI (digitalizado)";
            var (pdf, motivo) = ArquivoComoPdf(titulo, termo.Conteudo, termo.Nome, obraNome, obraLogo);
            adicionar(SecaoEpi, titulo, null, pdf, motivo);
        }
    }

    private async Task AdicionarAsosAsync(Guid trabalhadorId, string obraNome, byte[]? obraLogo, AdicionarDocumento adicionar, CancellationToken ct)
    {
        var asos = await _db.Asos.AsNoTracking()
            .NoEscopoDoTrabalhador(_db, a => a.TrabalhadorId)
            .Where(a => a.TrabalhadorId == trabalhadorId && _db.ArquivosAso.Any(x => x.AsoId == a.Id))
            .Select(a => new { a.Id, a.Tipo, a.DataExame })
            .ToListAsync(ct);

        foreach (var aso in asos)
        {
            var titulo = aso.Tipo switch
            {
                TipoExameAso.Admissional => "ASO Admissional",
                TipoExameAso.Periodico => "ASO Periódico",
                TipoExameAso.MudancaDeFuncao => "ASO de Mudança de Função",
                TipoExameAso.RetornoAoTrabalho => "ASO de Retorno ao Trabalho",
                TipoExameAso.Demissional => "ASO Demissional",
                _ => $"ASO {aso.Tipo}",
            };
            var arquivo = await _mediator.Send(new ObterArquivoAsoQuery(aso.Id), ct);
            var (pdf, motivo) = arquivo is null
                ? ((byte[]?)null, "Arquivo do ASO não encontrado.")
                : ArquivoComoPdf(titulo, arquivo.Conteudo, arquivo.NomeArquivo, obraNome, obraLogo);
            adicionar(SecaoAso, titulo, aso.DataExame, pdf, motivo);
        }
    }

    // Anexo enviado pelo usuário: PDF entra como está; foto vira uma página; o resto (Word, etc.) fica
    // de fora com o nome do arquivo no índice.
    private (byte[]? Pdf, string Motivo) ArquivoComoPdf(string titulo, byte[] conteudo, string nomeArquivo, string obraNome, byte[]? obraLogo)
    {
        if (conteudo.Length >= 4 && conteudo[0] == '%' && conteudo[1] == 'P' && conteudo[2] == 'D' && conteudo[3] == 'F')
            return (conteudo, string.Empty);

        var imagem = _pdf.ImagemComoPdf(titulo, conteudo, obraNome, obraLogo);
        return imagem is not null
            ? (imagem, string.Empty)
            : (null, $"Arquivo \"{nomeArquivo}\" em formato que não pode ser juntado ao PDF.");
    }
}
