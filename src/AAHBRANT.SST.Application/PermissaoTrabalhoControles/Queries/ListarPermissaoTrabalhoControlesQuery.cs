using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.PermissoesTrabalho;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoControles.Queries;

public record ListarPermissaoTrabalhoControlesQuery(Guid PermissaoTrabalhoId) : IRequest<List<PermissaoTrabalhoControleDto>>;

public class ListarPermissaoTrabalhoControlesQueryHandler : IRequestHandler<ListarPermissaoTrabalhoControlesQuery, List<PermissaoTrabalhoControleDto>>
{
    private readonly IAppDbContext _db;

    public ListarPermissaoTrabalhoControlesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PermissaoTrabalhoControleDto>> Handle(ListarPermissaoTrabalhoControlesQuery request, CancellationToken ct)
    {
        return await _db.PermissaoTrabalhoControles
            .Where(c => c.PermissaoTrabalhoId == request.PermissaoTrabalhoId)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new PermissaoTrabalhoControleDto
            {
                Id = c.Id,
                PermissaoTrabalhoId = c.PermissaoTrabalhoId,
                Descricao = c.Descricao
            })
            .ToListAsync(ct);
    }
}
