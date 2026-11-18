using System.Security.Cryptography;
using System.Text;
using AAHBRANT.SST.Application.Cameras;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

// Chave carregada uma única vez em DependencyInjection.AddInfrastructure (seção "Lgpd"). Diferente das outras chaves,
// a ausência NÃO derruba a inicialização da API: só o cadastro/uso de senha de câmera falha, com mensagem clara. Assim
// o deploy não quebra o resto do sistema se o segredo do ambiente ainda não foi criado.
public static class CredencialCameraCriptografiaContexto
{
    public static byte[] Chave { get; private set; } = Array.Empty<byte>();

    public static void Configurar(byte[] chave) => Chave = chave;
}

// Mesmo formato do CpfCriptografiaConversor: base64 de nonce (12) + texto cifrado + tag (16).
public class CredencialCameraCriptografiaService : ICredencialCameraCriptografia
{
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;
    private readonly byte[]? _chaveFixa;

    // Produção usa a chave do contexto (carregada da configuração); a sobrecarga com chave existe para os testes.
    public CredencialCameraCriptografiaService() { }
    public CredencialCameraCriptografiaService(byte[] chave) => _chaveFixa = chave;

    private byte[] Chave() => _chaveFixa ?? CredencialCameraCriptografiaContexto.Chave;

    private const string ChaveAusente =
        "Chave de criptografia das credenciais de câmera não configurada (Lgpd:ChaveCriptografiaCredenciaisCameraBase64).";

    public string Cifrar(string senhaPlana)
    {
        var chave = Chave();
        if (chave.Length == 0) throw new InvalidOperationException(ChaveAusente);

        var textoPlano = Encoding.UTF8.GetBytes(senhaPlana);
        var nonce = RandomNumberGenerator.GetBytes(TamanhoNonce);
        var cifrado = new byte[textoPlano.Length];
        var tag = new byte[TamanhoTag];

        using var aesGcm = new AesGcm(chave, TamanhoTag);
        aesGcm.Encrypt(nonce, textoPlano, cifrado, tag);

        var resultado = new byte[TamanhoNonce + cifrado.Length + TamanhoTag];
        Buffer.BlockCopy(nonce, 0, resultado, 0, TamanhoNonce);
        Buffer.BlockCopy(cifrado, 0, resultado, TamanhoNonce, cifrado.Length);
        Buffer.BlockCopy(tag, 0, resultado, TamanhoNonce + cifrado.Length, TamanhoTag);
        return Convert.ToBase64String(resultado);
    }

    public string? Decifrar(string senhaCifrada)
    {
        if (string.IsNullOrEmpty(senhaCifrada)) return null;
        var chave = Chave();
        if (chave.Length == 0) throw new InvalidOperationException(ChaveAusente);

        try
        {
            var bytes = Convert.FromBase64String(senhaCifrada);
            var nonce = bytes[..TamanhoNonce];
            var tag = bytes[^TamanhoTag..];
            var cifrado = bytes[TamanhoNonce..^TamanhoTag];
            var textoPlano = new byte[cifrado.Length];

            using var aesGcm = new AesGcm(chave, TamanhoTag);
            aesGcm.Decrypt(nonce, cifrado, tag, textoPlano);
            return Encoding.UTF8.GetString(textoPlano);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            Console.Error.WriteLine($"[LGPD] Falha ao decifrar credencial de câmera (chave divergente ou dado corrompido): {ex.GetType().Name}");
            return null;
        }
    }
}
