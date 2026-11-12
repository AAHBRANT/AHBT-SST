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
