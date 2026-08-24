using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.PermissoesTrabalho;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoRequisitos.Queries;

public record ListarPermissaoTrabalhoRequisitosQuery(Guid PermissaoTrabalhoId) : IRequest<List<PermissaoTrabalhoRequisitoDto>>;

public class ListarPermissaoTrabalhoRequisitosQueryHandler : IRequestHandler<ListarPermissaoTrabalhoRequisitosQuery, List<PermissaoTrabalhoRequisitoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPermissaoTrabalhoRequisitosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PermissaoTrabalhoRequisitoDto>> Handle(ListarPermissaoTrabalhoRequisitosQuery request, CancellationToken ct)
    {
        return await _db.PermissaoTrabalhoRequisitos
            .Where(r => r.PermissaoTrabalhoId == request.PermissaoTrabalhoId)
            .OrderBy(r => r.CreatedAtUtc)
            .Select(r => new PermissaoTrabalhoRequisitoDto
            {
                Id = r.Id,
                PermissaoTrabalhoId = r.PermissaoTrabalhoId,
                Descricao = r.Descricao,
                Atendido = r.Atendido
            })
            .ToListAsync(ct);
    }
}
