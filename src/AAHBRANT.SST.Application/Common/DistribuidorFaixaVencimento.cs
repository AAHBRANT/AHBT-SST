namespace AAHBRANT.SST.Application.Common;

// Distribui datas de vencimento (treinamento/ASO/prazo de NC) em 3 faixas determinísticas por
// índice — nunca aleatório, para o seeder de dados mocados ser 100% reprodutível e testável.
// Proporção pedida na spec da Fase 1: ~20% vencido, ~25% a vencer em breve, ~55% válido.
public static class DistribuidorFaixaVencimento
{
    public enum Faixa
    {
        Vencido,
        AVencerEmBreve,
        Valido,
    }

    public static Faixa ObterFaixa(int indice)
    {
        var posicao = ((indice % 20) + 20) % 20;
        if (posicao < 4) return Faixa.Vencido;         // 4/20 = 20%
        if (posicao < 9) return Faixa.AVencerEmBreve;  // 5/20 = 25%
        return Faixa.Valido;                             // 11/20 = 55%
    }

    public static DateTime CalcularData(int indice, DateTime referenciaUtc)
    {
        var posicao = ((indice % 20) + 20) % 20;
        var bloco = (int)Math.Floor(indice / 20.0);
        // Combina bloco + posicao para garantir diversidade 0-9 em qualquer faixa, independente da distribuição
        var variacao = (((bloco + posicao) % 10) + 10) % 10;

        return ObterFaixa(indice) switch
        {
            Faixa.Vencido => referenciaUtc.AddDays(-(5 + variacao * 6)),          // 5 a 59 dias no passado
            Faixa.AVencerEmBreve => referenciaUtc.AddDays(1 + variacao * 3),      // 1 a 28 dias no futuro
            Faixa.Valido => referenciaUtc.AddDays(31 + variacao * 33),            // 31 a 328 dias no futuro
            _ => referenciaUtc,
        };
    }
}
