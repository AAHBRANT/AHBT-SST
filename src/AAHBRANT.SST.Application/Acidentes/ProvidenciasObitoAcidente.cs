using System.Text.Json;

namespace AAHBRANT.SST.Application.Acidentes;

// Checklist genérico de providências quando a ocorrência tem óbito. É um apoio de organização, não um
// parecer jurídico: NÃO traz prazos legais nem afirma obrigatoriedades. Os itens que dependem de norma ou
// prazo dizem "confirmar com o jurídico". Gravado como JSON em Acidente.ProvidenciasObitoJson; ler/gravar
// só por aqui.
public static class ProvidenciasObitoAcidente
{
    public const int MaximoObservacao = 300;

    private static readonly (string Codigo, string Descricao)[] Padrao =
    {
        ("acionar-diretoria-juridico", "Comunicar a Diretoria e o Jurídico da empresa"),
        ("familia", "Definir quem faz o contato com a família e registrar o contato"),
        ("preservar-local", "Preservar o local e as evidências até a liberação pelas autoridades"),
        ("autoridades", "Comunicar às autoridades competentes (confirmar com o jurídico quais, como e em que prazo)"),
        ("cat", "Avaliar a emissão da CAT quando houver vínculo de emprego (confirmar com o jurídico)"),
        ("seguro-contratante", "Comunicar a seguradora e a empresa contratante, se houver (confirmar com o jurídico)"),
        ("laudo", "Acompanhar o laudo e anexá-lo à investigação"),
    };

    private sealed record ItemGravado(string Codigo, bool Concluida, DateTime? ConcluidaEm, string? Observacao);

    public static IReadOnlyCollection<string> CodigosValidos => Padrao.Select(p => p.Codigo).ToArray();

    // Sempre devolve todos os itens padrão, na ordem fixa, com o que já foi marcado.
    public static List<ProvidenciaObitoDto> Ler(string? json)
    {
        var gravados = new Dictionary<string, ItemGravado>();
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                foreach (var item in JsonSerializer.Deserialize<List<ItemGravado>>(json) ?? new())
                    gravados[item.Codigo] = item;
            }
            catch (JsonException)
            {
                // JSON corrompido: volta ao checklist em branco em vez de derrubar a tela da ocorrência.
            }
        }

        return Padrao.Select(p =>
        {
            gravados.TryGetValue(p.Codigo, out var g);
            return new ProvidenciaObitoDto(p.Codigo, p.Descricao, g?.Concluida ?? false, g?.ConcluidaEm, g?.Observacao);
        }).ToList();
    }

    // Itens com código desconhecido são ignorados; a data de conclusão é preservada se o item já estava
    // marcado, e zerada ao desmarcar.
    public static string Gravar(string? jsonAtual, IEnumerable<(string Codigo, bool Concluida, string? Observacao)> itens, DateTime agoraUtc)
    {
        var atuais = Ler(jsonAtual).ToDictionary(i => i.Codigo);
        var resultado = new List<ItemGravado>();
        var informados = itens.GroupBy(i => i.Codigo).ToDictionary(g => g.Key, g => g.Last());
        foreach (var codigo in CodigosValidos)
        {
            var atual = atuais[codigo];
            if (!informados.TryGetValue(codigo, out var novo))
            {
                resultado.Add(new ItemGravado(codigo, atual.Concluida, atual.ConcluidaEm, atual.Observacao));
                continue;
            }
            var obs = string.IsNullOrWhiteSpace(novo.Observacao) ? null : novo.Observacao.Trim();
            var quando = novo.Concluida ? (atual.Concluida ? atual.ConcluidaEm : agoraUtc) : null;
            resultado.Add(new ItemGravado(codigo, novo.Concluida, quando, obs));
        }
        return JsonSerializer.Serialize(resultado);
    }
}
