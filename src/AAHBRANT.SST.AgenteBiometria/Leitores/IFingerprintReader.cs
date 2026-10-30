namespace AAHBRANT.SST.AgenteBiometria.Leitores;

public interface IFingerprintReader
{
    // exigirNovoToque: se já houver um dedo no leitor, espera ele sair e só aceita uma nova apoiada —
    // usado na segunda leitura do cadastro, para não confirmar a digital com a mesma imagem ainda no vidro.
    // aceitar: quando informado, cada imagem lida passa por ele e o leitor só devolve a primeira aceita —
    // a assinatura compara com o cache a cada foto, sem depender do sensor de presença (dedo seco/fino).
    // Se nenhuma for aceita até o tempo limite, devolve a última lida (o servidor recusa pelo limiar).
    Task<byte[]> CapturarAsync(CancellationToken ct, bool exigirNovoToque = false, Func<byte[], bool>? aceitar = null);
}
