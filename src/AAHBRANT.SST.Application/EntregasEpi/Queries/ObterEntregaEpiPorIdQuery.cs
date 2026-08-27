using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Queries;

public record ObterEntregaEpiPorIdQuery(Guid Id) : IRequest<EntregaEpiDto?>;

public class ObterEntregaEpiPorIdQueryHandler : IRequestHandler<ObterEntregaEpiPorIdQuery, EntregaEpiDto?>
{
    private readonly IAppDbContext _db;
    public ObterEntregaEpiPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EntregaEpiDto?> Handle(ObterEntregaEpiPorIdQuery request, CancellationToken ct)
        => await _db.EntregasEpi
            .Where(x => x.Id == request.Id)
            .Select(x => new EntregaEpiDto(
                x.Id,
                x.TrabalhadorId,
                x.CatalogoEpiId,
                x.DataEntrega,
                x.DataDevolucao,
                x.DataValidade,
                x.Quantidade,
                x.QuantidadeDevolucao,
                x.VistoConsorcioResponsavel,
                x.Motivo,
                x.Observacoes,
                x.MotivoTipo,
                x.NumeroListaPresencaNr6,
                x.DataTreinamentoNr6))
            .FirstOrDefaultAsync(ct);
}
