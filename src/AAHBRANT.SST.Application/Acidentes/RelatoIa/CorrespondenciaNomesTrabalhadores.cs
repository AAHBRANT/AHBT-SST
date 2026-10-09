using System.Globalization;
using System.Text;

namespace AAHBRANT.SST.Application.Acidentes.RelatoIa;

/// <summary>
/// Localiza no cadastro da obra os funcionários citados pelo nome no relato. A comparação é feita
/// aqui, no servidor, para a lista de funcionários nunca ser enviada à IA (LGPD).
/// </summary>
public static class CorrespondenciaNomesTrabalhadores
{
    public record Candidato(Guid Id, string Nome);

    public record Resultado(IReadOnlyList<Guid> Localizados, IReadOnlyList<string> Pendencias);

    // Um nome citado só é aceito quando aponta para exatamente um funcionário: todas as palavras
    // ditas ("João Pedro") aparecem no nome cadastrado ("João Pedro da Silva"). Ambiguidade ou
    // ausência viram pendência para o técnico resolver na lista — nunca um palpite.
    public static Resultado Localizar(IEnumerable<string> nomesCitados, IReadOnlyList<Candidato> candidatos)
    {
        var localizados = new List<Guid>();
        var pendencias = new List<string>();
        var candidatosNormalizados = candidatos.Select(c => (c.Id, Palavras: Palavras(c.Nome))).ToList();

        foreach (var citado in nomesCitados.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var palavrasCitadas = Palavras(citado);
            if (palavrasCitadas.Count == 0)
                continue;

            var encontrados = candidatosNormalizados
                .Where(c => palavrasCitadas.All(p => c.Palavras.Contains(p)))
                .Select(c => c.Id)
                .ToList();

            if (encontrados.Count == 1)
            {
                if (!localizados.Contains(encontrados[0]))
                    localizados.Add(encontrados[0]);
            }
            else if (encontrados.Count == 0)
            {
                pendencias.Add($"\"{citado.Trim()}\" foi citado no relato, mas não foi encontrado no cadastro desta obra. Selecione o funcionário na lista.");
            }
            else
            {
                pendencias.Add($"\"{citado.Trim()}\" corresponde a {encontrados.Count} funcionários desta obra. Selecione o correto na lista.");
            }
        }

        return new Resultado(localizados, pendencias);
    }

    private static HashSet<string> Palavras(string nome)
    {
        var semAcento = new StringBuilder();
        foreach (var c in nome.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                semAcento.Append(char.ToLowerInvariant(c));
        }

        // Partículas ("da", "de", "dos") não identificam ninguém e são omitidas na fala.
        return semAcento.ToString()
            .Split(new[] { ' ', '-', '.', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !Particulas.Contains(p))
            .ToHashSet();
    }

    private static readonly HashSet<string> Particulas = new() { "da", "de", "do", "das", "dos", "e" };
}
