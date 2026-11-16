using System.Diagnostics;

namespace AAHBRANT.SST.AgenteCameras.Infra;

public interface IRelogio
{
    DateTimeOffset Agora { get; }
    Task AtrasarAsync(TimeSpan tempo, CancellationToken ct);
}

public sealed class RelogioSistema : IRelogio
{
    public DateTimeOffset Agora => DateTimeOffset.Now;
    public Task AtrasarAsync(TimeSpan tempo, CancellationToken ct) => Task.Delay(tempo, ct);
}

// Relógio monotônico (não muda se alguém acertar a data/hora do PC): usado para medir prazos relativos, como o do ao vivo.
public interface IRelogioMonotonico
{
    TimeSpan Decorrido { get; }
    Task AtrasarAsync(TimeSpan tempo, CancellationToken ct);
}

public sealed class RelogioMonotonicoSistema : IRelogioMonotonico
{
    public TimeSpan Decorrido => Stopwatch.GetElapsedTime(0, Stopwatch.GetTimestamp());
    public Task AtrasarAsync(TimeSpan tempo, CancellationToken ct) => Task.Delay(tempo, ct);
}
