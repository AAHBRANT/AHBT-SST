using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

// LGPD (12/10): fotos de rosto e de digital são dado pessoal sensível (art. 5º II, art. 46) e ficavam em
// texto puro no banco. Este conversor cifra com AES-256-GCM ao gravar e decifra ao ler, então o resto do
// sistema continua vendo os bytes da imagem. A chave é derivada (HKDF) da chave de criptografia do CPF, que
// fica só no servidor — não reaproveita a chave dos templates, que também vai para os agentes das obras.
// Valores gravados antes desta mudança (sem o prefixo) são lidos como estão; a rotina
// CriptografiaFotosBackgroundService regrava esses registros já cifrados.
public class FotoBiometricaCriptografiaConversor : ValueConverter<byte[], byte[]>
{
    private static readonly byte[] Prefixo = Encoding.ASCII.GetBytes("SSTFOTO1");
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;
    private static byte[]? _chave;

    public FotoBiometricaCriptografiaConversor() : base(v => Cifrar(v), v => Decifrar(v)) { }

    // Compara pelo conteúdo: sem isto o EF compara referência do array e não percebe uma foto trocada.
    public static readonly ValueComparer<byte[]> Comparador = new(
        (a, b) => Iguais(a, b),
        v => CodigoHash(v),
        v => v == null ? null! : v.ToArray());

    private static bool Iguais(byte[]? a, byte[]? b) => a == null ? b == null : b != null && a.AsSpan().SequenceEqual(b);

    private static int CodigoHash(byte[]? v) => v == null || v.Length == 0 ? 0 : HashCode.Combine(v.Length, v[0], v[v.Length - 1]);

    private static byte[] Chave()
    {
        if (_chave is not null)
            return _chave;
        if (CpfCriptografiaContexto.ChaveCriptografia.Length == 0)
            throw new InvalidOperationException("Chave de criptografia (Lgpd:ChaveCriptografiaCpfBase64) não carregada: não dá para cifrar as fotos biométricas.");
        return _chave = HKDF.DeriveKey(
            HashAlgorithmName.SHA256, CpfCriptografiaContexto.ChaveCriptografia, 32, info: Encoding.ASCII.GetBytes("sst-fotos-biometricas"));
    }

    public static bool EstaCifrado(byte[] valor) => valor.Length >= Prefixo.Length && valor.AsSpan(0, Prefixo.Length).SequenceEqual(Prefixo);

    public static byte[] Cifrar(byte[] imagem)
    {
        if (imagem.Length == 0 || EstaCifrado(imagem))
            return imagem;
        var nonce = RandomNumberGenerator.GetBytes(TamanhoNonce);
        var cifrado = new byte[imagem.Length];
        var tag = new byte[TamanhoTag];
        using (var aes = new AesGcm(Chave(), TamanhoTag))
            aes.Encrypt(nonce, imagem, cifrado, tag);
        return [.. Prefixo, .. nonce, .. cifrado, .. tag];
    }

    public static byte[] Decifrar(byte[] armazenado)
    {
        if (!EstaCifrado(armazenado))
            return armazenado;
        var dados = armazenado.AsSpan(Prefixo.Length);
        var nonce = dados[..TamanhoNonce];
        var tag = dados[^TamanhoTag..];
        var cifrado = dados[TamanhoNonce..^TamanhoTag];
        var imagem = new byte[cifrado.Length];
        using (var aes = new AesGcm(Chave(), TamanhoTag))
            aes.Decrypt(nonce, cifrado, tag, imagem);
        return imagem;
    }
}
