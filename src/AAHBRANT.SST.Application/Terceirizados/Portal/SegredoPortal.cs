using System.Security.Cryptography;
using System.Text;

namespace AAHBRANT.SST.Application.Terceirizados.Portal;

// Segredos do portal de terceiros: link do contrato, código por e-mail e sessão. Só o hash vai para o
// banco (AcessoPortalTerceiro). Aleatório via CSPRNG, mesmo raciocínio de TokenValidacaoPublicaGerador.
public static class SegredoPortal
{
    // 32 bytes (256 bits) em hex: inviável de adivinhar, então o link sozinho já não é enumerável — o
    // código por e-mail é a segunda barreira, para um link repassado não bastar.
    public static string GerarToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string GerarCodigo() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string Hash(string valor) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor.Trim())));

    // O código tem só 6 dígitos: o hash leva o id do acesso junto para o mesmo código não ter o mesmo
    // hash em acessos diferentes.
    public static string HashCodigo(Guid acessoId, string codigo) => Hash($"{acessoId:N}:{codigo}");

    public static bool HashConfere(string? hashGravado, string hashCalculado) =>
        hashGravado is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hashGravado), Encoding.ASCII.GetBytes(hashCalculado));

    // "w*****@empresa.com.br" — a tela de entrada mostra para onde o código foi sem expor o e-mail
    // inteiro a quem só tem o link.
    public static string MascararEmail(string email)
    {
        var arroba = email.IndexOf('@');
        if (arroba <= 0) return "***";
        return $"{email[0]}{new string('*', Math.Max(arroba - 1, 3))}{email[arroba..]}";
    }
}
