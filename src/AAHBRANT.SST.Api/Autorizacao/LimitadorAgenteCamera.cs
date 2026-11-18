using System.Threading.RateLimiting;

namespace AAHBRANT.SST.Api.Autorizacao;

// Limite por agente de câmeras, aplicado DEPOIS da autenticação pelo segredo: só chamada de agente autenticado conta
// na cota dele. Quem não tem o segredo (ou chuta GUIDs) nunca chega aqui e portanto não esgota a cota de um agente
// legítimo; contra esse tráfego vale o limite por IP da policy "agente-camera-ip" (rate limiter do ASP.NET, que
// roda antes do controller). 600/min cobre ~6 câmeras a 1 quadro a cada 2 s (180/min) com folga para o reenvio do
// que ficou guardado sem internet. Singleton: o estado da janela precisa sobreviver entre requisições.
public sealed class LimitadorAgenteCamera : IDisposable
{
    public const int LimitePadraoPorMinuto = 600;

    private readonly PartitionedRateLimiter<Guid> _limitador;

    public LimitadorAgenteCamera(int limitePorMinuto = LimitePadraoPorMinuto)
    {
        _limitador = PartitionedRateLimiter.Create<Guid, Guid>(agenteId =>
            RateLimitPartition.GetFixedWindowLimiter(agenteId, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limitePorMinuto,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    }

    // Verdadeiro = a chamada cabe na cota do agente.
    public bool TentarObter(Guid agenteId)
    {
        using var permissao = _limitador.AttemptAcquire(agenteId);
        return permissao.IsAcquired;
    }

    public void Dispose() => _limitador.Dispose();
}
