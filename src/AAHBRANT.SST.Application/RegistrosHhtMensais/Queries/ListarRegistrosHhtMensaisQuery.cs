using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RegistrosHhtMensais.Queries;

public record ListarRegistrosHhtMensaisQuery(Guid? ObraId, int? Ano) : IRequest<List<RegistroHhtMensalDto>>;

public class ListarRegistrosHhtMensaisQueryHandler
    : IRequestHandler<ListarRegistrosHhtMensaisQuery, List<RegistroHhtMensalDto>>
{
    private readonly IAppDbContext _db;

    public ListarRegistrosHhtMensaisQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<RegistroHhtMensalDto>> Handle(ListarRegistrosHhtMensaisQuery request, CancellationToken ct)
    {
        var query = _db.RegistrosHhtMensais.AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(r => r.ObraId == request.ObraId.Value);

        if (request.Ano.HasValue)
            query = query.Where(r => r.Ano == request.Ano.Value);

        return await query
            .Include(r => r.Obra)
            .OrderByDescending(r => r.Ano).ThenByDescending(r => r.Mes)
            .Select(r => new RegistroHhtMensalDto
            {
                Id = r.Id,
                ObraId = r.ObraId,
                ObraNome = r.Obra != null ? r.Obra.Nome : null,
                Ano = r.Ano,
                Mes = r.Mes,
                HorasHomemTrabalhadas = r.HorasHomemTrabalhadas,
            })
            .ToListAsync(ct);
    }
}
