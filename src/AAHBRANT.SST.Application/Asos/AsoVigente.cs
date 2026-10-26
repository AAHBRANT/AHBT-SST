using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Asos;

// Regra única do ASO vigente (decisão do usuário, 26/10/2026), usada por todas as telas, consultas e alertas:
//  1. O ASO vigente de uma pessoa é o do EXAME MAIS RECENTE. Os demais são "superados": nunca aparecem como
//     vencidos ou pendentes e nunca bloqueiam.
//  2. Empate de data de exame (documentos repetidos no G-RH): vale o que tem parecer sobre o Pendente; entre os que
//     têm parecer, o Inapto (lado da segurança); depois a maior validade; por último o Id, só para o resultado nunca
//     depender da ordem em que o banco devolveu as linhas.
//  3. O vigente decide a liberação: precisa ser Apto ou Apto com restrição e estar dentro da validade. Pendente
//     nunca libera, e o exame mais novo manda mesmo havendo um Apto anterior ainda válido.
public enum SituacaoAso
{
    SemAso,
    Liberado,
    Vencido,
    SemParecer,
    Inapto,
}

public readonly record struct AsoResumo(Guid Id, Guid TrabalhadorId, DateTime DataExame, DateTime DataValidade, ResultadoAso Resultado);

public static class AsoVigente
{
    // Ordem em que o vigente vem primeiro e os superados depois, do mais recente ao mais antigo.
    public static readonly IComparer<AsoResumo> Comparador = Comparer<AsoResumo>.Create(Comparar);

    private static int Comparar(AsoResumo a, AsoResumo b)
    {
        var c = b.DataExame.Date.CompareTo(a.DataExame.Date);
        if (c != 0) return c;
        c = TemParecer(b).CompareTo(TemParecer(a));
        if (c != 0) return c;
        c = (b.Resultado == ResultadoAso.Inapto).CompareTo(a.Resultado == ResultadoAso.Inapto);
        if (c != 0) return c;
        c = b.DataValidade.Date.CompareTo(a.DataValidade.Date);
        if (c != 0) return c;
        return b.Id.CompareTo(a.Id);
    }

    private static bool TemParecer(AsoResumo aso) => aso.Resultado != ResultadoAso.Pendente;

    public static AsoResumo? Eleger(IEnumerable<AsoResumo> asosDaPessoa)
    {
        AsoResumo? vigente = null;
        foreach (var aso in asosDaPessoa)
            if (vigente is null || Comparar(aso, vigente.Value) < 0) vigente = aso;
        return vigente;
    }

    public static Dictionary<Guid, AsoResumo> PorTrabalhador(IEnumerable<AsoResumo> asos)
        => asos.GroupBy(a => a.TrabalhadorId).ToDictionary(g => g.Key, g => Eleger(g)!.Value);

    public static SituacaoAso Classificar(AsoResumo? vigente, DateTime hoje)
    {
        if (vigente is not { } aso) return SituacaoAso.SemAso;
        // Vencimento vence qualquer parecer; o parecer só decide quando o exame ainda vale.
        if (aso.DataValidade.Date < hoje.Date) return SituacaoAso.Vencido;
        return aso.Resultado switch
        {
            ResultadoAso.Apto or ResultadoAso.AptoComRestricao => SituacaoAso.Liberado,
            ResultadoAso.Inapto => SituacaoAso.Inapto,
            _ => SituacaoAso.SemParecer,
        };
    }

    public static bool EstaLiberado(AsoResumo? vigente, DateTime hoje) => Classificar(vigente, hoje) == SituacaoAso.Liberado;

    // Texto do badge de aptidão (perfil, ficha e página pública do QR), sempre pelo ASO vigente.
    public static string RotuloAptidao(AsoResumo? vigente, DateTime hoje) => Classificar(vigente, hoje) switch
    {
        SituacaoAso.SemAso => "Sem ASO",
        SituacaoAso.Vencido => "ASO vencido",
        SituacaoAso.SemParecer => "Pendente",
        SituacaoAso.Inapto => "Inapto",
        _ => vigente!.Value.Resultado == ResultadoAso.AptoComRestricao ? "Apto com restrição" : "Apto",
    };

    // Marca em cada DTO se é o vigente da pessoa e, nos superados, a data do exame que o substituiu. Só funciona
    // com todos os ASOs de cada pessoa na lista (as consultas filtram por pessoa ou obra, nunca por ASO).
    public static void Marcar(IReadOnlyList<AsoDto> asos)
    {
        var vigentes = PorTrabalhador(asos.Select(Resumo));
        foreach (var dto in asos)
        {
            var vigente = vigentes[dto.TrabalhadorId];
            dto.Vigente = vigente.Id == dto.Id;
            dto.SubstituidoPorDataExame = dto.Vigente ? null : vigente.DataExame;
        }
    }

    public static AsoResumo Resumo(AsoDto dto) => new(dto.Id, dto.TrabalhadorId, dto.DataExame, dto.DataValidade, dto.ResultadoStatus);
}
