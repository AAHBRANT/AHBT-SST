using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Usuarios.Queries;

public record ListarUsuariosQuery(StatusUsuario? Status = null) : IRequest<List<UsuarioDto>>;

public class ListarUsuariosQueryHandler : IRequestHandler<ListarUsuariosQuery, List<UsuarioDto>>
{
    private readonly IAppDbContext _db;

    public ListarUsuariosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<UsuarioDto>> Handle(ListarUsuariosQuery request, CancellationToken ct)
    {
        var query = _db.Usuarios.AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(u => u.Status == request.Status.Value);

        return await query
            .OrderBy(u => u.Nome)
            .Select(u => new UsuarioDto
            {
                Id = u.Id,
                AzureAdObjectId = u.AzureAdObjectId,
                Email = u.Email,
                Nome = u.Nome,
                Status = u.Status,
                UltimoLoginUtc = u.UltimoLoginUtc,
                TrabalhadorId = u.TrabalhadorId,
                PerfisPorObra = u.PerfisPorObra.Select(pp => new UsuarioPerfilObraDto
                {
                    Id = pp.Id,
                    PerfilAcessoId = pp.PerfilAcessoId,
                    PerfilAcessoNome = pp.PerfilAcesso!.Nome,
                    ObraId = pp.ObraId,
                    ObraNome = pp.Obra != null ? pp.Obra.Nome : null
                }).ToList()
            })
            .ToListAsync(ct);
    }
}
