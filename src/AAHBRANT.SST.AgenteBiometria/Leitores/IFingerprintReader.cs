namespace AAHBRANT.SST.AgenteBiometria.Leitores;

public interface IFingerprintReader
{
    Task<byte[]> CapturarAsync(CancellationToken ct);
}
