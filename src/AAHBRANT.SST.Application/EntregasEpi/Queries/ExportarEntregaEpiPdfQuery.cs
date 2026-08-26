using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Queries;

public record ExportarEntregaEpiPdfQuery(Guid Id) : IRequest<byte[]?>;

public class ExportarEntregaEpiPdfQueryHandler : IRequestHandler<ExportarEntregaEpiPdfQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly IEntregaEpiPdfService _pdf;

    public ExportarEntregaEpiPdfQueryHandler(IAppDbContext db, IEntregaEpiPdfService pdf)
    {
        _db = db;
        _pdf = pdf;
    }

    public async Task<byte[]?> Handle(ExportarEntregaEpiPdfQuery request, CancellationToken ct)
    {
        var entrega = await _db.EntregasEpi
            .Include(e => e.Trabalhador!).ThenInclude(t => t.Obra)
            .Include(e => e.Trabalhador!).ThenInclude(t => t.Funcao)
            .Include(e => e.CatalogoEpi)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        if (entrega is null) return null;

        var modelo = new EntregaEpiPdfModelo(
            entrega.Trabalhador?.Obra?.Nome ?? string.Empty,
            entrega.Trabalhador?.Nome ?? string.Empty,
            entrega.Trabalhador?.Matricula ?? string.Empty,
            entrega.Trabalhador?.Funcao?.Nome ?? string.Empty,
            entrega.CatalogoEpi?.Nome ?? string.Empty,
            entrega.CatalogoEpi?.Fabricante,
            entrega.CatalogoEpi?.CertificadoAprovacaoNumero,
            entrega.DataEntrega,
            entrega.DataDevolucao,
            entrega.DataValidade,
            entrega.Quantidade,
            entrega.QuantidadeDevolucao,
            entrega.VistoConsorcioResponsavel,
            entrega.Motivo,
            entrega.Observacoes);

        return _pdf.Gerar(modelo);
    }
}
