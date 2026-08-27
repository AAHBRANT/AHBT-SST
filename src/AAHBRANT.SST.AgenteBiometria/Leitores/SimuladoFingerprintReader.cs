namespace AAHBRANT.SST.AgenteBiometria.Leitores;

// Implementação simulada — sem o SDK Futronic real (ScanAPI/ftrapi) não há hardware para capturar.
// Usada em desenvolvimento/testes; troca por uma implementação real via P/Invoke assim que o FS80H
// físico e o SDK chegarem (fora do escopo deste plano — ver spec §2 "Não entra").
public class SimuladoFingerprintReader : IFingerprintReader
{
    private readonly byte[] _proximaCaptura;

    public SimuladoFingerprintReader(byte[] proximaCaptura)
    {
        _proximaCaptura = proximaCaptura;
    }

    public Task<byte[]> CapturarAsync(CancellationToken ct) => Task.FromResult(_proximaCaptura);
}
