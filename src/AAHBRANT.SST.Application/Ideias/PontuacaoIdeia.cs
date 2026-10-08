using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Ideias;

// Especificação §9: Impacto + Urgência + Valor para o negócio + Esforço/Complexidade. A nota é só
// apoio à decisão — a prioridade final é sempre definida pelo gestor.
// Fórmula (decisão nossa, ajustável): ganho = (3*Impacto + 2*Urgência + 3*Valor) / 8, cada nível
// normalizado para 0..1; custo = (Esforço + Complexidade) / 2, idem. Nota = 100 * (0,75*ganho + 0,25*(1-custo)).
public static class PontuacaoIdeia
{
    public static int? Calcular(NivelIdeia? impacto, NivelIdeia? urgencia, NivelIdeia? valor,
        NivelIdeia? esforco, NivelIdeia? complexidade)
    {
        if (impacto is null || urgencia is null || valor is null || esforco is null || complexidade is null)
            return null;

        var ganho = (3 * Norm(impacto.Value) + 2 * Norm(urgencia.Value) + 3 * Norm(valor.Value)) / 8.0;
        var custo = (Norm(esforco.Value) + Norm(complexidade.Value)) / 2.0;
        return (int)Math.Round(100 * (0.75 * ganho + 0.25 * (1 - custo)), MidpointRounding.AwayFromZero);
    }

    public static PrioridadeIdeia? SugerirPrioridade(int? pontuacao)
        => pontuacao switch
        {
            null => null,
            >= 67 => PrioridadeIdeia.P1,
            >= 40 => PrioridadeIdeia.P2,
            _ => PrioridadeIdeia.P3
        };

    private static double Norm(NivelIdeia n) => ((int)n - 1) / 2.0;
}
