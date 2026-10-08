namespace AAHBRANT.SST.Application.Ideias;

// Especificação §16 — detecta ideias parecidas por sobreposição de palavras relevantes (coeficiente
// de Dice: 2·|A∩B| / (|A|+|B|), mais estável que Jaccard em textos curtos). É um apoio: quem decide vincular ou criar nova é o usuário.
public static class SimilaridadeIdeias
{
    public const double Limiar = 0.45;

    private static readonly HashSet<string> Irrelevantes = new(StringComparer.Ordinal)
    {
        "para","com","uma","uns","umas","que","por","nos","nas","dos","das","mais","como","pelo","pela",
        "deve","deveria","poderia","seria","ser","sistema","quando","esta","esse","essa","isso","ele","ela",
        "tem","ter","ao","aos","em","de","da","do","um","os","as","se","na","no","e","ou","seja","sao","fosse",
        "interessante","acho","gostaria","queria","modulo"
    };

    public static HashSet<string> Palavras(string texto)
    {
        var norm = HeuristicaIdeiaEstruturacaoService.Normalizar(texto);
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var atual = new System.Text.StringBuilder();
        foreach (var c in norm + " ")
        {
            if (char.IsLetterOrDigit(c)) { atual.Append(c); continue; }
            if (atual.Length >= 3)
            {
                var palavra = atual.ToString();
                if (!Irrelevantes.Contains(palavra)) tokens.Add(Radical(palavra));
            }
            atual.Clear();
        }
        return tokens;
    }

    public static double Calcular(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var inter = a.Count(b.Contains);
        return 2.0 * inter / (a.Count + b.Count);
    }

    // Radical rudimentar (prefixo de 5 letras) para casar "vencido"/"vencimento"/"vencidos".
    private static string Radical(string p) => p.Length > 5 ? p[..5] : p;
}
