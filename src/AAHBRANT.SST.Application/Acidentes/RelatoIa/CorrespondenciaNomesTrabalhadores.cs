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

    // Externas: pessoas que a IA chamou de externas e que realmente não batem com nenhum funcionário da obra.
    public record Resultado(
        IReadOnlyList<Guid> Localizados,
        IReadOnlyList<string> Pendencias,
        IReadOnlyList<PessoaExternaCitadaIa> Externas,
        // Subconjunto de Localizados que veio de pessoas que a IA chamou de externas (provável erro da IA).
        IReadOnlyList<Guid> LocalizadosDeExternas);

    // Um nome citado só é aceito quando aponta para exatamente um funcionário: todas as palavras
    // ditas ("João Pedro") aparecem no nome cadastrado ("João Pedro da Silva"). Ambiguidade ou
    // ausência viram pendência para o técnico resolver na lista — nunca um palpite.
    //
    // Pessoas que a IA classificou como externas passam pela mesma regra: se o nome bate com UM
    // funcionário da obra, a IA errou e a pessoa vira funcionário localizado; sem correspondência
    // (ou ambígua), continua externa. O aviso "não encontrado no cadastro" vale só para quem a IA
    // chamou de funcionário — um nome que a IA também listou como externo não gera esse aviso.
    public static Resultado Localizar(
        IEnumerable<string> nomesCitados,
        IReadOnlyList<Candidato> candidatos,
        IEnumerable<PessoaExternaCitadaIa>? pessoasExternas = null)
    {
        var localizados = new List<Guid>();
        var pendencias = new List<string>();
        var externas = new List<PessoaExternaCitadaIa>();
        var candidatosNormalizados = candidatos.Select(c => (c.Id, Palavras: Palavras(c.Nome))).ToList();

        var localizadosDeExternas = new List<Guid>();
        var nomesExternosMantidos = new List<HashSet<string>>();
        foreach (var externa in pessoasExternas ?? Array.Empty<PessoaExternaCitadaIa>())
        {
            var palavras = Palavras(externa.Nome ?? string.Empty);
            if (palavras.Count == 0)
                continue;

            // Só o primeiro nome ("seu João") não basta para transformar uma pessoa externa em funcionário.
            var encontrados = palavras.Count < 2
                ? new List<Guid>()
                : candidatosNormalizados
                    .Where(c => palavras.All(p => c.Palavras.Contains(p)))
                    .Select(c => c.Id)
                    .ToList();

            if (encontrados.Count == 1)
            {
                if (!localizados.Contains(encontrados[0]))
                    localizados.Add(encontrados[0]);
                if (!localizadosDeExternas.Contains(encontrados[0]))
                    localizadosDeExternas.Add(encontrados[0]);
                var funcionario = candidatos.First(c => c.Id == encontrados[0]).Nome;
                pendencias.Add($"\"{externa.Nome!.Trim()}\" foi citado como pessoa externa, mas corresponde ao funcionário \"{funcionario}\" desta obra. Confirme.");
            }
            else
            {
                if (encontrados.Count > 1)
                    pendencias.Add($"\"{externa.Nome!.Trim()}\" foi citado como pessoa externa, mas pode ser um dos {encontrados.Count} funcionários desta obra; confira.");
                externas.Add(externa);
                nomesExternosMantidos.Add(palavras);
            }
        }

        // Quem a IA já listou como externo (e continua externo) não pode virar "funcionário não encontrado":
        // vale nome igual ou um contido no outro ("Severino" x "Severino Joaquim do Nascimento").
        nomesCitados = nomesCitados.Where(n =>
        {
            if (string.IsNullOrWhiteSpace(n)) return false;
            var pc = Palavras(n);
            return pc.Count == 0 || !nomesExternosMantidos.Any(e => pc.IsSubsetOf(e) || e.IsSubsetOf(pc));
        });

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

        return new Resultado(localizados, pendencias, externas, localizadosDeExternas);
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
