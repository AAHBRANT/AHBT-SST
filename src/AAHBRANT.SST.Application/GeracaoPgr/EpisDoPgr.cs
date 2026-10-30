using System.Text.RegularExpressions;
using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Tipo de EPI que o PGR exige e a especificação técnica que vai na relação de EPIs por função.
public record TipoEpiPgr(string Nome, string Especificacao);

// Reconhece o tipo de EPI no texto das medidas que a IA escreve ("Use capacete ao circular…"). O PGR manda
// (pedido do usuário, 30/10/2026): a relação de EPIs sai só das medidas dos riscos do GHE, sem catálogo nem matriz do
// cadastro, e sem CA — o CA é do produto entregue e fica na ficha de entrega (NR-06). As especificações são o padrão
// de mercado e devem ser conferidas pelo Engenheiro de Segurança antes de assinar.
public static class EpisDoPgr
{
    private sealed record Regra(TipoEpiPgr Tipo, Regex Padrao, string[] Remover);

    private static Regra R(string nome, string especificacao, string padrao, params string[] remover) =>
        new(new TipoEpiPgr(nome, especificacao), new Regex(padrao, RegexOptions.CultureInvariant), remover);

    // Ordem = ordem na tabela. Termos em "Remover" saem do texto antes de testar a regra (bota de PVC não é botina,
    // máscara de solda não é respirador).
    private static readonly Regra[] Regras =
    {
        R("Capacete de segurança", "Classe B, com jugular · ABNT NBR 8221", @"\bcapacetes?\b"),
        R("Calçado de segurança", "Botina com biqueira e solado antiderrapante · ABNT NBR ISO 20345",
            @"\b(calcados?|botinas?|botas?|sapatos?)\b", "bota de pvc", "botas de pvc", "bota impermeavel", "botas impermeaveis", "bota de borracha", "botas de borracha"),
        R("Bota impermeável", "PVC, cano longo, solado antiderrapante", @"\bbotas? (de pvc|impermeave(l|is)|de borracha)\b"),
        R("Luva de segurança", "Proteção contra agentes mecânicos · ABNT NBR ISO 21420 / EN 388", @"\bluvas?\b", "luva isolante", "luvas isolantes"),
        R("Luva isolante", "Borracha, classe compatível com a tensão da instalação", @"\bluvas? isolantes?\b"),
        R("Óculos de segurança", "Lente incolor, com proteção lateral", @"\boculos\b"),
        R("Protetor facial", "Viseira incolor contra impacto", @"\b(protetor facial|viseiras?)\b"),
        R("Máscara de solda", "Lente com tonalidade compatível com o processo de soldagem", @"\b(mascaras? de solda|escudos? de solda)\b"),
        R("Protetor auricular", "Plugue ou concha, atenuação compatível com o ruído medido",
            @"\b(protetor(es)? auricular(es)?|protecao auditiva|abafador(es)?|plugs?|plugues?|tampoes? auditivos?)\b"),
        R("Respirador PFF2", "Peça semifacial filtrante · ABNT NBR 13698", @"\b(respirador(es)?|pff ?[23]|mascaras?)\b", "mascara de solda", "mascaras de solda"),
        R("Colete refletivo", "Alta visibilidade · ABNT NBR 15292", @"\b(coletes?|refletiv[oa]s?|alta visibilidade)\b"),
        R("Cinturão paraquedista", "Tipo paraquedista, com talabarte duplo e absorvedor de energia · ABNT NBR 15836 / 15834",
            @"\b(cintos?|cinturao|cinturoes|talabartes?|trava[- ]?quedas?)\b"),
        R("Avental de raspa", "Raspa de couro", @"\baventa(l|is)\b"),
        R("Perneira de segurança", "Raspa de couro ou material sintético", @"\bperneiras?\b"),
        R("Mangote de raspa", "Raspa de couro", @"\bmangotes?\b"),
        R("Protetor solar", "FPS 30 ou superior", @"\b(protetor solar|filtro solar|bloqueador solar)\b"),
    };

    // Tipos citados no texto, na ordem da tabela. Texto que é medida de EPI mas não cita nenhum tipo conhecido devolve vazio.
    public static IReadOnlyList<TipoEpiPgr> Reconhecer(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return Array.Empty<TipoEpiPgr>();
        var normal = FuncaoSstClassifier.Normalizar(texto);
        var tipos = new List<TipoEpiPgr>();
        foreach (var r in Regras)
        {
            var alvo = r.Remover.Aggregate(normal, (t, termo) => t.Replace(termo, " "));
            if (r.Padrao.IsMatch(alvo)) tipos.Add(r.Tipo);
        }
        return tipos;
    }

    // Posição do tipo na tabela, para ordenar as linhas de cada função.
    public static int Ordem(string nome) => Array.FindIndex(Regras, r => r.Tipo.Nome == nome) is var i and >= 0 ? i : int.MaxValue;
}
