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

    // Mantém os de maior P×S até o teto, preservando a ordem original entre os escolhidos.
    public static List<RiscoGerado> LimitarPorAtividade(IReadOnlyList<RiscoGerado> riscos, int vagas)
    {
        if (vagas <= 0) return new();
        if (riscos.Count <= vagas) return riscos.ToList();
        var escolhidos = riscos
            .Select((r, i) => (r, i))
            .OrderByDescending(x => x.r.Probabilidade * x.r.Severidade)
            .ThenBy(x => x.i)
            .Take(vagas)
            .Select(x => x.i)
            .ToHashSet();
        return riscos.Where((_, i) => escolhidos.Contains(i)).ToList();
    }
}
