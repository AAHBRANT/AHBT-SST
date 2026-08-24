using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Usuarios.Queries;

public record ObterUsuarioPorIdQuery(Guid Id) : IRequest<UsuarioDto?>;

public class ObterUsuarioPorIdQueryHandler : IRequestHandler<ObterUsuarioPorIdQuery, UsuarioDto?>
{
    private readonly IAppDbContext _db;

    public ObterUsuarioPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<UsuarioDto?> Handle(ObterUsuarioPorIdQuery request, CancellationToken ct)
    {
        return await _db.Usuarios
            .Where(u => u.Id == request.Id)
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
            .FirstOrDefaultAsync(ct);
    }
}
