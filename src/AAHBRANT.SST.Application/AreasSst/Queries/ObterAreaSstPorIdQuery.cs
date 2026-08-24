using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AreasSst.Queries;

public record ObterAreaSstPorIdQuery(Guid Id) : IRequest<AreaSstDto?>;

public class ObterAreaSstPorIdQueryHandler : IRequestHandler<ObterAreaSstPorIdQuery, AreaSstDto?>
{
    private readonly IAppDbContext _db;

    public ObterAreaSstPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AreaSstDto?> Handle(ObterAreaSstPorIdQuery request, CancellationToken ct)
    {
        return await _db.AreasSst
            .Where(a => a.Id == request.Id)
            .Select(a => new AreaSstDto
            {
                Id = a.Id,
                Codigo = a.Codigo,
                Nome = a.Nome,
                Tipo = a.Tipo,
                ObraId = a.ObraId,
                DetalhesLocalizacao = a.DetalhesLocalizacao,
                Riscos = a.Riscos,
                Requisitos = a.Requisitos,
                Status = a.Status
            })
            .FirstOrDefaultAsync(ct);
    }
}
