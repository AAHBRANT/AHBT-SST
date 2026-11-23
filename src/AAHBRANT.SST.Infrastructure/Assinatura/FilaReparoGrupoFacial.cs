using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Pedido de conferência do grupo facial de uma obra, feito na hora em que uma identificação falha de um
// jeito que pode ser grupo quebrado ("cadastro sem vínculo" ou "rosto não reconhecido"). Quem atende é o
// ReparoGrupoFacialBackgroundService, fora da requisição de quem está assinando.
public interface IFilaReparoGrupoFacial
{
    void Solicitar(Guid obraId);
}

// Guardião do grupo facial (23/11): na 2ª vez em que a Beira Rio ficou com os vínculos quebrados, a obra
// passou a manhã falhando até alguém rodar "recriar grupo" na mão. Com a fila, a primeira falha suspeita já
// dispara a conferência (e a reconstrução, se precisar). A mesma obra entra no máximo uma vez a cada
// IntervaloMinimoPorObra, para uma fila de DDS com várias falhas seguidas não virar várias conferências.
public sealed class FilaReparoGrupoFacial : IFilaReparoGrupoFacial
{
    public static readonly TimeSpan IntervaloMinimoPorObra = TimeSpan.FromMinutes(30);

    private readonly Channel<Guid> _canal = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, DateTime> _ultimoPedido = new();
    private readonly Func<DateTime> _agoraUtc;

    public FilaReparoGrupoFacial() : this(() => DateTime.UtcNow) { }

    public FilaReparoGrupoFacial(Func<DateTime> agoraUtc) => _agoraUtc = agoraUtc;

    public void Solicitar(Guid obraId)
    {
        var agora = _agoraUtc();
        var aceito = false;
        _ultimoPedido.AddOrUpdate(obraId,
            _ => { aceito = true; return agora; },
            (_, ultimo) =>
            {
                if (agora - ultimo < IntervaloMinimoPorObra) return ultimo;
                aceito = true;
                return agora;
            });
        if (aceito)
            _canal.Writer.TryWrite(obraId);
    }

    public bool TentarRetirar(out Guid obraId) => _canal.Reader.TryRead(out obraId);

    public ValueTask<Guid> RetirarAsync(CancellationToken ct) => _canal.Reader.ReadAsync(ct);
}
