using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pgrs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PlanoAcao.Queries;

public record ListarPlanoAcaoItensQuery(Guid PgrId) : IRequest<List<PlanoAcaoItemDto>>;

public class ListarPlanoAcaoItensQueryHandler : IRequestHandler<ListarPlanoAcaoItensQuery, List<PlanoAcaoItemDto>>
{
    private readonly IAppDbContext _db;

    public ListarPlanoAcaoItensQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PlanoAcaoItemDto>> Handle(ListarPlanoAcaoItensQuery request, CancellationToken ct)
    {
        var itens = await _db.PlanoAcaoItens
            .Where(i => i.PgrId == request.PgrId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(ct);

        return itens.Select(i => new PlanoAcaoItemDto
        {
            Id = i.Id,
            PgrId = i.PgrId,
            RiscoId = i.RiscoId,
            Descricao = i.Descricao,
            ResponsavelUsuarioId = i.ResponsavelUsuarioId,
            Prazo = i.Prazo,
            DataConclusao = i.DataConclusao,
            Status = i.Status
        }).ToList();
    }
}
