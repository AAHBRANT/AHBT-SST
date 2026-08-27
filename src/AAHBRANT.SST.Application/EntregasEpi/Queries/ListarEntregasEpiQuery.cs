using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Queries;

public record ListarEntregasEpiQuery(Guid? TrabalhadorId = null) : IRequest<List<EntregaEpiDto>>;

public class ListarEntregasEpiQueryHandler : IRequestHandler<ListarEntregasEpiQuery, List<EntregaEpiDto>>
{
    private readonly IAppDbContext _db;
    public ListarEntregasEpiQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<EntregaEpiDto>> Handle(ListarEntregasEpiQuery request, CancellationToken ct)
    {
        var query = _db.EntregasEpi.AsQueryable();
        if (request.TrabalhadorId is not null)
            query = query.Where(x => x.TrabalhadorId == request.TrabalhadorId);

        return await query
            .OrderByDescending(x => x.DataEntrega)
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
            .ToListAsync(ct);
    }
}
