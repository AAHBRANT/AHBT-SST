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

    public ExportarFichaEpiTrabalhadorQueryHandler(IAppDbContext db, IFichaEpiPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarFichaEpiTrabalhadorQuery request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores
            .Include(t => t.Obra)
            .Include(t => t.Funcao)
            .FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct);
        if (trabalhador is null) return null;

        var entregas = await _db.EntregasEpi
            .Include(e => e.CatalogoEpi)
            .Where(e => e.TrabalhadorId == request.TrabalhadorId)
            .OrderBy(e => e.DataEntrega)
            .ToListAsync(ct);

        var entregaIds = entregas.Select(e => e.Id).ToList();

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

            var assinadoPeloEmpregado = documentosEntrega.TryGetValue(entrega.Id, out var docEntrega)
                && docEntrega.Signatarios.Any(s => s.TrabalhadorId == entrega.TrabalhadorId);
            var assinadoPeloResponsavel = docEntrega is not null
                && docEntrega.Signatarios.Any(s => s.MetodoAutenticacao == MetodoAutenticacaoAssinatura.SessaoLogada);

            linhasEntrega.Add(new LinhaEntregaEpiPdf(
                numero,
                entrega.CatalogoEpi?.Nome ?? string.Empty,
                entrega.CatalogoEpi?.CertificadoAprovacaoNumero,
                entrega.MotivoTipo,
                entrega.Motivo,
                entrega.Quantidade,
                entrega.DataEntrega,
                assinadoPeloEmpregado,
                assinadoPeloResponsavel));

            if (entrega.DataDevolucao is null) continue;

            var assinadoDevolucaoPeloEmpregado = documentosDevolucao.TryGetValue(entrega.Id, out var docDevolucao)
                && docDevolucao.Signatarios.Any(s => s.TrabalhadorId == entrega.TrabalhadorId);

            linhasDevolucao.Add(new LinhaDevolucaoEpiPdf(
                numero,
                entrega.CatalogoEpi?.Nome ?? string.Empty,
                entrega.QuantidadeDevolucao ?? entrega.Quantidade,
                entrega.DataDevolucao.Value,
                assinadoDevolucaoPeloEmpregado,
                entrega.VistoConsorcioResponsavel));
        }

        // Chave sintética "FichaEpiTrabalhador"/TrabalhadorId: a Ficha agrega N entregas, cada uma já
        // individualmente rastreável (DocumentoAssinatura por entrega, acima) — ninguém assina a Ficha
        // em si, esta rastreabilidade é só pra atestar integridade do PDF impresso como um todo.
        var rastreio = await _rastreabilidade.GarantirAsync("FichaEpiTrabalhador", request.TrabalhadorId, ct);

        var modelo = new FichaEpiPdfModelo(
            trabalhador.Obra?.Nome ?? string.Empty,
            trabalhador.Obra?.Cliente,
            trabalhador.Obra?.Cnpj,
            trabalhador.Obra?.LogoConteudo,
            trabalhador.Obra?.LogoContentType,
            trabalhador.Nome,
            CpfMascarador.Mascarar(trabalhador.Cpf),
            trabalhador.Matricula,
            trabalhador.Funcao?.Nome ?? string.Empty,
            trabalhador.Turno,
            trabalhador.DataAdmissao,
            linhasEntrega,
            linhasDevolucao,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng);

        return _pdf.Gerar(modelo);
    }
}
