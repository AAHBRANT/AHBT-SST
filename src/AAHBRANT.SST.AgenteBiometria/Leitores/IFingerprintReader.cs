namespace AAHBRANT.SST.AgenteBiometria.Leitores;

public interface IFingerprintReader
{
    // exigirNovoToque: se já houver um dedo no leitor, espera ele sair e só aceita uma nova apoiada —
    // usado na segunda leitura do cadastro, para não confirmar a digital com a mesma imagem ainda no vidro.
    Task<byte[]> CapturarAsync(CancellationToken ct, bool exigirNovoToque = false);
}
