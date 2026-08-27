using System.Text.RegularExpressions;

namespace AAHBRANT.SST.Application.Common;

// Espelha src/AAHBRANT.SST.TeamsApp/src/lib/cpf.ts — mesma regra de exibição (LGPD): só os 2
// dígitos verificadores finais ficam visíveis por padrão.
public static partial class CpfMascarador
{
    public static string Formatar(string valor)
    {
        var digitos = SomenteDigitos().Replace(valor, string.Empty);
        if (digitos.Length > 11) digitos = digitos[..11];

        var resultado = digitos.Length > 3 ? digitos[..3] : digitos;
        if (digitos.Length > 3) resultado += $".{digitos[3..Math.Min(6, digitos.Length)]}";
        if (digitos.Length > 6) resultado += $".{digitos[6..Math.Min(9, digitos.Length)]}";
        if (digitos.Length > 9) resultado += $"-{digitos[9..Math.Min(11, digitos.Length)]}";
        return resultado;
    }

    public static string Mascarar(string valor)
    {
        var digitos = SomenteDigitos().Replace(valor, string.Empty);
        if (digitos.Length < 11) return Formatar(digitos);
        return $"***.***.***-{digitos[^2..]}";
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex SomenteDigitos();
}
