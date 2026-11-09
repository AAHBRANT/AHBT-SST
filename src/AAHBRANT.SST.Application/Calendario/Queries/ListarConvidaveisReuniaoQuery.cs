using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Calendario.Queries;

// Pessoas que podem ser convidadas para uma reunião criada pelo Calendário: usuários ativos com
// e-mail. Só nome e e-mail (o mesmo que o próprio Outlook mostra no catálogo da empresa) — a lista
// completa de usuários (/api/usuarios) é restrita à administração.
public record ListarConvidaveisReuniaoQuery : IRequest<List<ConvidavelReuniaoDto>>;

public record ConvidavelReuniaoDto(Guid Id, string Nome, string Email);

public class ListarConvidaveisReuniaoQueryHandler(IAppDbContext db)
    : IRequestHandler<ListarConvidaveisReuniaoQuery, List<ConvidavelReuniaoDto>>
{
    public Task<List<ConvidavelReuniaoDto>> Handle(ListarConvidaveisReuniaoQuery request, CancellationToken ct)
        => db.Usuarios.AsNoTracking()
            .Where(u => u.Status == StatusUsuario.Ativo && u.Email != "")
            .OrderBy(u => u.Nome)
            .Select(u => new ConvidavelReuniaoDto(u.Id, u.Nome, u.Email))
            .ToListAsync(ct);
}
