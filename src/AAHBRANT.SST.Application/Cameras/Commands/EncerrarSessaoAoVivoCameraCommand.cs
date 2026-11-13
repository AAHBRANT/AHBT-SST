using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Tela: "fechei". NÃO encerra na hora (a sessão é compartilhada: outro espectador pode estar olhando): antecipa a inatividade,
// gravando UltimaVisualizacaoEmUtc para que a sessão encerre por inatividade em ~10 s se ninguém mais buscar quadro. Quem
// buscar quadro nesse intervalo regrava o horário atual (a regra dos 5 s do espectador já o permite, pois o horário gravado
// aqui é bem mais antigo) e a sessão segue. Nunca adia: se a última visualização já era mais antiga, não mexe. Sessão fora do
// escopo do usuário ou inexistente = KeyNotFoundException (404); já encerrada = nada a fazer (idempotente).
public record EncerrarSessaoAoVivoCameraCommand(Guid SessaoId) : IRequest;

public class EncerrarSessaoAoVivoCameraCommandHandler : IRequestHandler<EncerrarSessaoAoVivoCameraCommand>
{
    private readonly IAppDbContext _db;
    private readonly AoVivoCameraOpcoes _opcoes;

    public EncerrarSessaoAoVivoCameraCommandHandler(IAppDbContext db, IOptions<AoVivoCameraOpcoes>? opcoes = null)
    {
        _db = db;
        _opcoes = opcoes?.Value ?? new AoVivoCameraOpcoes();
    }

    public async Task Handle(EncerrarSessaoAoVivoCameraCommand request, CancellationToken ct)
    {
        // Leitura com o filtro global: é ela que confere o escopo por obra. Sem trazer o quadro.
        var existe = await _db.SessoesAoVivoCamera.AsNoTracking().AnyAsync(s => s.Id == request.SessaoId, ct);
        if (!existe) throw new KeyNotFoundException($"Sessão ao vivo {request.SessaoId} não encontrada.");

        var agora = DateTime.UtcNow;
        // Última visualização "como se" fosse de InatividadeSegundos - EncerrarAoFecharEmSegundos atrás: a inatividade vence daqui a ~10 s.
        var alvo = agora.AddSeconds(-Math.Max(0, _opcoes.InatividadeSegundos - _opcoes.EncerrarAoFecharEmSegundos));
        await _db.SessoesAoVivoCamera.IgnoreQueryFilters()
            .Where(s => s.Id == request.SessaoId && s.EncerradaEmUtc == null && s.UltimaVisualizacaoEmUtc > alvo)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.UltimaVisualizacaoEmUtc, alvo), ct);
    }
}
