using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Obras.Queries;

public record ListarObrasQuery : IRequest<List<ObraDto>>;

public class ListarObrasQueryHandler : IRequestHandler<ListarObrasQuery, List<ObraDto>>
{
    private readonly IAppDbContext _db;

    public ListarObrasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ObraDto>> Handle(ListarObrasQuery request, CancellationToken ct)
    {
        return await _db.Obras
            .OrderBy(o => o.Nome)
            .Select(o => new ObraDto
            {
                Id = o.Id,
                Codigo = o.Codigo,
                Nome = o.Nome,
                Cliente = o.Cliente,
                Status = o.Status,
                DataInicio = o.DataInicio,
                DataPrevisaoTermino = o.DataPrevisaoTermino,
                DataTerminoReal = o.DataTerminoReal,
                Endereco = o.Endereco,
                Cidade = o.Cidade,
                Uf = o.Uf
            })
            .ToListAsync(ct);
    }
}
