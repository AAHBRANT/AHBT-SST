using System.Globalization;
using System.Text;

namespace AAHBRANT.SST.Application.Common;

public static class FuncaoSstClassifier
{
    public const string TecnicoSeguranca = "Técnico de Segurança";

    public static bool EhTecnicoSeguranca(string? nomeFuncao)
    {
        if (string.IsNullOrWhiteSpace(nomeFuncao)) return false;

        var normalizado = Normalizar(nomeFuncao);
        return normalizado == "tecnico de seguranca"
            || normalizado.StartsWith("tecnico de seguranca ", StringComparison.Ordinal);
    }

    private static string Normalizar(string valor)
    {
        var semAcentos = valor.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(semAcentos.Length);

        foreach (var c in semAcentos)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }

        return string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
