using System.Security.Cryptography;
using System.Text;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

// Diferente de PinHasher (PBKDF2+salt, necessário para um PIN de 4-6 dígitos de baixa entropia),
// o segredo do dispositivo é gerado aqui mesmo com 256 bits de aleatoriedade — SHA-256 simples já
// é suficiente, já que não há risco de força bruta por dicionário.
public static class SegredoDispositivoHasher
{
    public static string GerarSegredo()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    public static string GerarHash(string segredo)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(segredo));
        return Convert.ToBase64String(bytes);
    }

    public static bool Verificar(string segredo, string hash)
    {
        var hashCalculado = Convert.FromBase64String(GerarHash(segredo));
        var hashEsperado = Convert.FromBase64String(hash);
        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
    }
}
