using AAHBRANT.SST.Application.Aprs;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Aprs;

// Cobre exatamente a fórmula da aba "Matriz de Risco" da planilha APR REV.02 (1-4 Baixo,
// 5-9 Moderado, 10-15 Alto, 16-25 Crítico), inclusive nos limites de cada faixa.
public class AprNivelRiscoCalculatorTests
{
    [Theory]
    [InlineData(1, 1, NivelRiscoApr.Baixo)]
    [InlineData(2, 2, NivelRiscoApr.Baixo)] // 4 — limite superior de Baixo
    [InlineData(1, 5, NivelRiscoApr.Moderado)] // 5 — limite inferior de Moderado
    [InlineData(3, 3, NivelRiscoApr.Moderado)] // 9 — limite superior de Moderado
    [InlineData(2, 5, NivelRiscoApr.Alto)] // 10 — limite inferior de Alto
    [InlineData(3, 5, NivelRiscoApr.Alto)] // 15 — limite superior de Alto
    [InlineData(4, 4, NivelRiscoApr.Critico)] // 16 — limite inferior de Crítico
    [InlineData(5, 5, NivelRiscoApr.Critico)] // 25 — máximo possível
    public void Calcular_RetornaNivelCorretoNosLimitesDeCadaFaixa(int probabilidade, int severidade, NivelRiscoApr esperado)
    {
        var resultado = AprNivelRiscoCalculator.Calcular(probabilidade, severidade);

        Assert.Equal(esperado, resultado);
    }
}
