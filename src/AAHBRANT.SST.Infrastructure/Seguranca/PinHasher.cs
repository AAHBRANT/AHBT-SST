using System.Security.Cryptography;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

// Hash do PIN de reserva (crachá/QR + PIN — método de fallback do Motor de Assinatura Eletrônica,
// docs/Motor-Assinatura-Eletronica.md §2/§3). Deliberadamente NÃO segue o padrão de
// CpfCriptografiaConversor.CalcularHash (HMAC-SHA256 com chave única compartilhada): um PIN de 4-6
// dígitos tem espaço de busca pequeno (10.000 a 1.000.000 combinações), então um HMAC simples seria
// forçável por força bruta em segundos caso a tabela vaze. PBKDF2 com salt aleatório por trabalhador
// e iteração alta é o padrão adequado para segredo de baixa entropia — mesma família de técnica usada
// para senhas. O hash é auto-contido (algoritmo+iterações+salt+hash na própria string), então não
// precisa de uma chave de aplicação configurada externamente como o CPF precisa.
public static class PinHasher
{
    private const int TamanhoSalt = 16;
    private const int TamanhoHash = 32;
    private const int Iteracoes = 210_000;

    public static string GerarHash(string pin)
    {
        if (string.IsNullOrEmpty(pin))
            throw new ArgumentException("PIN não pode ser vazio.", nameof(pin));

        var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
        return $"PBKDF2-SHA256${Iteracoes}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string pin, string pinHash)
    {
        var partes = pinHash.Split('$');
        if (partes.Length != 4 || partes[0] != "PBKDF2-SHA256")
            return false;

        var iteracoes = int.Parse(partes[1]);
        var salt = Convert.FromBase64String(partes[2]);
        var hashEsperado = Convert.FromBase64String(partes[3]);

        var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iteracoes, HashAlgorithmName.SHA256, hashEsperado.Length);
        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
    }
}
