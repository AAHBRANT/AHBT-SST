using System.Security.Cryptography;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

public static class TemplateBiometricoCriptografiaContexto
{
    private static byte[]? _chave;

    public static void Configurar(byte[] chave) => _chave = chave;

    internal static byte[] ObterChave() =>
        _chave ?? throw new InvalidOperationException(
            "TemplateBiometricoCriptografiaContexto não foi configurado. Chame Configurar() no startup.");
}

// Coluna opaca — sem HasConversion<T> do EF. Um ValueConverter descriptografaria automaticamente a
// cada leitura (ex.: SincronizarTemplatesQueryHandler), colocando bytes biométricos em texto puro na
// memória do backend a cada sincronização. Aqui a criptografia só acontece uma vez, no cadastro
// (CadastrarTemplateBiometricoCommandHandler), e o valor cifrado é tratado como opaco daí em diante —
// só o agente local, dono da mesma chave simétrica (distribuída fora de banda), consegue descriptografar.
public static class TemplateBiometricoCriptografiaConversor
{
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;

    public static string Criptografar(byte[] templateBruto)
    {
        var chave = TemplateBiometricoCriptografiaContexto.ObterChave();
        var nonce = RandomNumberGenerator.GetBytes(TamanhoNonce);
        var cifrado = new byte[templateBruto.Length];
        var tag = new byte[TamanhoTag];

        using var aesGcm = new AesGcm(chave, TamanhoTag);
        aesGcm.Encrypt(nonce, templateBruto, cifrado, tag);

        var resultado = new byte[TamanhoNonce + cifrado.Length + TamanhoTag];
        Buffer.BlockCopy(nonce, 0, resultado, 0, TamanhoNonce);
        Buffer.BlockCopy(cifrado, 0, resultado, TamanhoNonce, cifrado.Length);
        Buffer.BlockCopy(tag, 0, resultado, TamanhoNonce + cifrado.Length, TamanhoTag);

        return Convert.ToBase64String(resultado);
    }

    // Só usado em testes de round-trip — nenhum código de produção do backend chama isto.
    public static byte[] Descriptografar(string cifradoBase64)
    {
        var chave = TemplateBiometricoCriptografiaContexto.ObterChave();
        var bytes = Convert.FromBase64String(cifradoBase64);

        var nonce = bytes[..TamanhoNonce];
        var tag = bytes[^TamanhoTag..];
        var cifrado = bytes[TamanhoNonce..^TamanhoTag];
        var textoPlano = new byte[cifrado.Length];

        using var aesGcm = new AesGcm(chave, TamanhoTag);
        aesGcm.Decrypt(nonce, cifrado, tag, textoPlano);

        return textoPlano;
    }
}
