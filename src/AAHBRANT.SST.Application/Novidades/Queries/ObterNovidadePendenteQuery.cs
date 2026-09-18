using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Novidades.Queries;

public record ObterNovidadePendenteQuery(Guid? UsuarioId) : IRequest<NovidadeVersaoDto?>;

public class ObterNovidadePendenteQueryHandler : IRequestHandler<ObterNovidadePendenteQuery, NovidadeVersaoDto?>
{
    private readonly IAppDbContext _db;

    public ObterNovidadePendenteQueryHandler(IAppDbContext db) => _db = db;

    public async Task<NovidadeVersaoDto?> Handle(ObterNovidadePendenteQuery request, CancellationToken ct)
    {
        // Sem usuário resolvido não há como saber o que ele já viu — não mostra nada (fail-safe:
        // melhor não exibir pop-up do que exibir repetidamente por falta de identidade).
        if (request.UsuarioId is not { } usuarioId)
        {
            return null;
        }

        var novidade = await _db.NovidadesVersao
            .Include(n => n.Itens)
            .Where(n => !_db.NovidadesVisualizacao.Any(v => v.NovidadeVersaoId == n.Id && v.UsuarioId == usuarioId))
            .OrderByDescending(n => n.DataPublicacao)
            .ThenByDescending(n => n.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        return novidade is null ? null : NovidadesMapper.Mapear(novidade);
    }
}
