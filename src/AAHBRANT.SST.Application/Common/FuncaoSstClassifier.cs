using System.Globalization;
using System.Text;

namespace AAHBRANT.SST.Application.Common;

public static class FuncaoSstClassifier
{
    public const string TecnicoSeguranca = "Técnico de Segurança";

    public static bool EhTecnicoSeguranca(string? nomeFuncao)
    {
        if (string.IsNullOrWhiteSpace(nomeFuncao)) return false;

        var normalizado = NormalizarCargo(nomeFuncao);
        return normalizado == "tecnico de seguranca"
            || normalizado.StartsWith("tecnico de seguranca ", StringComparison.Ordinal);
    }

    public static bool EhEngenheiroSeguranca(string? nomeFuncao)
    {
        if (string.IsNullOrWhiteSpace(nomeFuncao)) return false;

        var normalizado = NormalizarCargo(nomeFuncao);
        return normalizado == "engenheiro de seguranca"
            || normalizado.StartsWith("engenheiro de seguranca ", StringComparison.Ordinal);
    }

    // Funções vindas do G-RH chegam abreviadas: "Tec. Segurança Do Trabalho" (Gilton, Beira Rio, 30/10)
    // era recusado na assinatura da inspeção. "Tec."/"Tec"/"Téc." vira "tecnico", "Eng."/"Eng" vira
    // "engenheiro" e "tecnico seguranca" (sem o "de") vira "tecnico de seguranca".
    public static string NormalizarCargo(string valor)
    {
        var palavras = Normalizar(valor).Replace(".", ". ").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (palavras.Count == 0) return string.Empty;

        palavras[0] = palavras[0] switch
        {
            "tec." or "tec" => "tecnico",
            "eng." or "eng" => "engenheiro",
            _ => palavras[0],
        };
        if (palavras.Count > 1 && palavras[1] == "seguranca" && palavras[0] is "tecnico" or "engenheiro")
            palavras.Insert(1, "de");
        return string.Join(' ', palavras);
    }

    public static string Normalizar(string valor)
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
