using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Novidades.Commands;

// Ao fechar o pop-up, marca como vista a novidade exibida E qualquer outra mais antiga que o
// usuário ainda não tivesse visto — evita empilhar pop-ups para quem ficou dias sem logar (decisão
// de design aprovada com o usuário: mostra sempre só a mais recente).
public record MarcarNovidadesComoVistasCommand(Guid? UsuarioId, Guid NovidadeVersaoId) : IRequest;

public class MarcarNovidadesComoVistasCommandHandler : IRequestHandler<MarcarNovidadesComoVistasCommand>
{
    private readonly IAppDbContext _db;

    public MarcarNovidadesComoVistasCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(MarcarNovidadesComoVistasCommand request, CancellationToken ct)
    {
        // Sem usuário resolvido (dev sem Entra ID, ou vínculo ainda não feito) não há onde gravar a
        // visualização — no-op silencioso, mesmo princípio de "nice to have" do VinculoAzureAdMiddleware.
        if (request.UsuarioId is not { } usuarioId)
        {
            return;
        }

        var referencia = await _db.NovidadesVersao
            .Where(n => n.Id == request.NovidadeVersaoId)
            .Select(n => n.DataPublicacao)
            .FirstOrDefaultAsync(ct);

        var idsJaVistos = await _db.NovidadesVisualizacao
            .Where(v => v.UsuarioId == usuarioId)
            .Select(v => v.NovidadeVersaoId)
            .ToListAsync(ct);

        var idsPendentes = await _db.NovidadesVersao
            .Where(n => n.DataPublicacao <= referencia && !idsJaVistos.Contains(n.Id))
            .Select(n => n.Id)
            .ToListAsync(ct);

        var agora = DateTime.UtcNow;
        foreach (var id in idsPendentes)
        {
            _db.NovidadesVisualizacao.Add(new NovidadeVisualizacao
            {
                NovidadeVersaoId = id,
                UsuarioId = usuarioId,
                VistoEmUtc = agora,
            });
        }

        if (idsPendentes.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }
}
