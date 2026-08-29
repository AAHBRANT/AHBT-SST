using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RequisitosLegais.Queries;

public record ListarRequisitosLegaisQuery(CategoriaRequisitoLegal? Categoria, StatusRequisitoLegal? Status) : IRequest<List<RequisitoLegalDto>>;

public class ListarRequisitosLegaisQueryHandler : IRequestHandler<ListarRequisitosLegaisQuery, List<RequisitoLegalDto>>
{
    private readonly IAppDbContext _db;

    public ListarRequisitosLegaisQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<RequisitoLegalDto>> Handle(ListarRequisitosLegaisQuery request, CancellationToken ct)
    {
        var query = _db.RequisitosLegais.AsQueryable();
        if (request.Categoria.HasValue) query = query.Where(r => r.Categoria == request.Categoria);
        if (request.Status.HasValue) query = query.Where(r => r.Status == request.Status);

        return await query
            .OrderBy(r => r.Norma).ThenBy(r => r.Artigo)
            .Select(r => new RequisitoLegalDto(r.Id, r.Norma, r.Artigo, r.Titulo, r.Descricao, r.Categoria, r.Status, r.Fonte))
            .ToListAsync(ct);
    }
}
