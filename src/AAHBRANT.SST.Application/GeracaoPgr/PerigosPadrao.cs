using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Nomes fixos de perigo usados pela IA no passo 4. Sem isso ela redige o mesmo perigo de formas diferentes
// ("Queda de materiais de níveis superiores" / "sobre pessoas abaixo") e o inventário não agrupa nem se revisa uma vez só.
public static class PerigosPadrao
{
    // Teto de perigos por atividade: o engenheiro revisa o inventário inteiro, mais que isso vira "aceitar tudo".
    public const int MaxRiscosPorAtividade = 5;

    public static readonly IReadOnlyList<(string Tipo, string Nome)> Catalogo = new (string, string)[]
    {
        ("Acidente", "Queda de altura"),
        ("Acidente", "Queda de materiais e ferramentas sobre pessoas"),
        ("Acidente", "Soterramento e desmoronamento em escavações e valas"),
        ("Acidente", "Choque elétrico"),
        ("Acidente", "Atropelamento e esmagamento por máquinas e veículos"),
        ("Acidente", "Prensamento e esmagamento de membros"),
        ("Acidente", "Corte e perfuração por ferramentas e materiais"),
        ("Acidente", "Projeção de partículas nos olhos"),
        ("Acidente", "Queda em mesmo nível e tropeço"),
        ("Acidente", "Incêndio e explosão"),
        ("Acidente", "Queimadura por solda, corte ou superfície quente"),
        ("Acidente", "Movimentação e içamento de cargas"),
        ("Acidente", "Tombamento de máquinas e equipamentos"),
        ("Físico", "Ruído"),
        ("Físico", "Vibração"),
        ("Físico", "Calor e radiação solar"),
        ("Físico", "Radiação não ionizante de solda"),
        ("Químico", "Poeira de cimento, sílica e materiais de obra"),
        ("Químico", "Fumos metálicos de solda"),
        ("Químico", "Vapores de tintas, solventes e resinas"),
        ("Químico", "Contato da pele com cimento e produtos químicos"),
        ("Biológico", "Animais peçonhentos e insetos"),
        ("Biológico", "Contato com água ou esgoto contaminado"),
        ("Ergonômico", "Esforço físico e levantamento de peso"),
        ("Ergonômico", "Postura inadequada e movimentos repetitivos"),
        ("Psicossocial", "Pressão por prazo e jornada extensa"),
    };

    public const string QuedaDeAltura = "Queda de altura";

    // Perigos que podem matar e nunca saem pelo teto de 5: no PGR da Beira Rio (17/11/2026) "Queda de altura"
    // perdia a vaga para calor, esforço físico e corte, que têm P×S parecido mas não matam.
    private static readonly HashSet<string> Graves = new[]
    {
        QuedaDeAltura,
        "Soterramento e desmoronamento em escavações e valas",
        "Choque elétrico",
        "Atropelamento e esmagamento por máquinas e veículos",
    }.Select(FuncaoSstClassifier.Normalizar).ToHashSet();

    public static bool EhGrave(string? perigo) =>
        !string.IsNullOrWhiteSpace(perigo) && Graves.Contains(FuncaoSstClassifier.Normalizar(perigo));

    // Ofícios que, em obra, costumam trabalhar acima de 2 m (fôrma, armação, alvenaria, pintura, estrutura metálica,
    // poste, cobertura, andaime). Serve de alerta, não de regra: quem confirma é o engenheiro (NR-35).
    private static readonly string[] OficiosEmAltura =
    {
        "pedreiro", "carpinteiro", "armador", "pintor", "serralheiro", "eletricista", "montador", "telhadista",
        "andaime", "soldador", "impermeabilizador", "gesseiro", "vidraceiro",
    };

    // Ajudante de um ofício (ajudante de eletricista) também sobe; o Ajudante Geral só auxilia e fica de fora.
    public static bool CostumaTrabalharEmAltura(string? funcao)
    {
        if (string.IsNullOrWhiteSpace(funcao)) return false;
        var palavras = FuncaoSstClassifier.Normalizar(funcao).Split(' ', '/', '-');
        return OficiosEmAltura.Any(palavras.Contains);
    }

    // Os graves ficam sempre, mesmo passando do teto (ou sem vaga, quando o técnico já fixou 5 riscos);
    // os demais entram por maior P×S nas vagas que sobrarem. Preserva a ordem original entre os escolhidos.
    public static List<RiscoGerado> LimitarPorAtividade(IReadOnlyList<RiscoGerado> riscos, int vagas)
    {
        if (riscos.Count <= Math.Max(vagas, 0)) return riscos.ToList();
        var graves = riscos.Select((r, i) => (r, i)).Where(x => EhGrave(x.r.Perigo)).Select(x => x.i).ToHashSet();
        var demais = riscos
            .Select((r, i) => (r, i))
            .Where(x => !graves.Contains(x.i))
            .OrderByDescending(x => x.r.Probabilidade * x.r.Severidade)
            .ThenBy(x => x.i)
            .Take(Math.Max(vagas - graves.Count, 0))
            .Select(x => x.i);
        graves.UnionWith(demais);
        return riscos.Where((_, i) => graves.Contains(i)).ToList();
    }
}
