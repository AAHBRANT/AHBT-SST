using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

// Chaves carregadas uma única vez em DependencyInjection.AddInfrastructure a partir da seção "Lgpd"
// da configuração. Mantidas como estado estático porque o EF Core instancia este conversor por
// reflection dentro de OnModelCreating, sem passar por injeção de dependência.
public static class CpfCriptografiaContexto
{
    public static byte[] ChaveCriptografia { get; private set; } = Array.Empty<byte>();
    public static byte[] ChaveHash { get; private set; } = Array.Empty<byte>();

    public static void Configurar(byte[] chaveCriptografia, byte[] chaveHash)
    {
        ChaveCriptografia = chaveCriptografia;
        ChaveHash = chaveHash;
    }
}

// Medida técnica de LGPD para Trabalhador.Cpf (art. 46): criptografia em nível de aplicação via
// AES-256-GCM, alternativa portável ao Always Encrypted (recurso exclusivo do SQL Server que exigiria
// provisionar Azure Key Vault — dependência de nuvem real, fora do escopo desta fatia). O nonce de 12
// bytes é gerado aleatoriamente a cada gravação, então o mesmo CPF produz ciphertext diferente a cada
// vez (proteção contra inferência por padrão repetido) — por isso a unicidade do CPF não pode mais ser
// garantida por índice sobre esta coluna; ver CalcularHash abaixo e a coluna Trabalhador.CpfHash.
public class CpfCriptografiaConversor : ValueConverter<string, string>
{
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;

    public CpfCriptografiaConversor() : base(v => Criptografar(v), v => Descriptografar(v))
    {
    }

    // Público para uso pelo CpfLgpdBackfillSeeder, que grava via ADO.NET cru (não passa pelo EF Core).
    public static string Criptografar(string cpfPlano)
    {
        if (string.IsNullOrEmpty(cpfPlano)) return string.Empty;

        var chave = CpfCriptografiaContexto.ChaveCriptografia;
        if (chave.Length == 0)
            throw new InvalidOperationException("Chave de criptografia do CPF não configurada (Lgpd:ChaveCriptografiaCpfBase64).");

        var textoPlano = Encoding.UTF8.GetBytes(cpfPlano);
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

    private static string Descriptografar(string cpfCifrado)
    {
        if (string.IsNullOrEmpty(cpfCifrado)) return string.Empty;

        var chave = CpfCriptografiaContexto.ChaveCriptografia;
        if (chave.Length == 0)
            throw new InvalidOperationException("Chave de criptografia do CPF não configurada (Lgpd:ChaveCriptografiaCpfBase64).");

        var bytes = Convert.FromBase64String(cpfCifrado);
        var nonce = bytes[..TamanhoNonce];
        var tag = bytes[^TamanhoTag..];
        var cifrado = bytes[TamanhoNonce..^TamanhoTag];
        var textoPlano = new byte[cifrado.Length];

        using var aesGcm = new AesGcm(chave, TamanhoTag);
        aesGcm.Decrypt(nonce, cifrado, tag, textoPlano);
        return Encoding.UTF8.GetString(textoPlano);
    }

    // HMAC-SHA256 determinístico (mesmo CPF → mesmo hash sempre), usado só para preservar a unicidade
    // de Cpf via índice em Trabalhador.CpfHash — nunca para recuperar o valor original.
    public static string CalcularHash(string cpfPlano)
    {
        var chave = CpfCriptografiaContexto.ChaveHash;
        if (chave.Length == 0)
            throw new InvalidOperationException("Chave de hash do CPF não configurada (Lgpd:ChaveHashCpfBase64).");

        using var hmac = new HMACSHA256(chave);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(cpfPlano));
        return Convert.ToHexString(hash);
    }
}
