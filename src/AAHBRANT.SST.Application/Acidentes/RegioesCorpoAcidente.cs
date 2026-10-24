using System.Text.RegularExpressions;

namespace AAHBRANT.SST.Application.Acidentes;

/// <summary>
/// Partes do corpo atingidas (mapa corporal da lesão). Código: "&lt;vista&gt;:&lt;parte&gt;[-lado]" — vista F
/// (frente) ou C (costas), ex.: "F:mao-esq", "C:lombar-dir". Os códigos válidos e os rótulos estão em
/// mapaCorporal.json (TeamsApp), o mesmo arquivo que a API usa para desenhar o mapa no PDF.
/// </summary>
public static partial class RegioesCorpoAcidente
{
    // O mapa tem 71 regiões; folga para regiões novas sem estourar a coluna (2000).
    public const int MaximoRegioes = 80;

    [GeneratedRegex(@"^[FC]:[a-z]+(-[a-z]+)*$")]
    private static partial Regex Formato();

    public static bool CodigoValido(string? codigo) => codigo is not null && Formato().IsMatch(codigo);

    public static string? Gravar(IEnumerable<string>? codigos)
    {
        var lista = (codigos ?? Enumerable.Empty<string>())
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return lista.Count == 0 ? null : string.Join(';', lista);
    }

    public static List<string> Ler(string? valor) => string.IsNullOrWhiteSpace(valor)
        ? new List<string>()
        : valor.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
