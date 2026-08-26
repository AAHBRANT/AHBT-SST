using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Application.Tests.Common;

public class DistribuidorFaixaVencimentoTests
{
    [Fact]
    public void ObterFaixa_DistribuicaoEmCadaBlocoDeVinte_DeveSeguirProporcaoDaSpec()
    {
        var faixas = Enumerable.Range(0, 20).Select(DistribuidorFaixaVencimento.ObterFaixa).ToList();

        Assert.Equal(4, faixas.Count(f => f == DistribuidorFaixaVencimento.Faixa.Vencido));
        Assert.Equal(5, faixas.Count(f => f == DistribuidorFaixaVencimento.Faixa.AVencerEmBreve));
        Assert.Equal(11, faixas.Count(f => f == DistribuidorFaixaVencimento.Faixa.Valido));
    }

    [Fact]
    public void CalcularData_ParaFaixaVencida_DeveRetornarDataNoPassadoDentroDoIntervaloDaSpec()
    {
        var referencia = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

        var indicesVencidos = Enumerable.Range(0, 20)
            .Where(i => DistribuidorFaixaVencimento.ObterFaixa(i) == DistribuidorFaixaVencimento.Faixa.Vencido);

        foreach (var indice in indicesVencidos)
        {
            var data = DistribuidorFaixaVencimento.CalcularData(indice, referencia);
            var diasNoPassado = (referencia - data).TotalDays;
            Assert.InRange(diasNoPassado, 5, 60);
        }
    }

    [Fact]
    public void CalcularData_ParaFaixaAVencerEmBreve_DeveRetornarDataFuturaDentroDeTrintaDias()
    {
        var referencia = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

        var indices = Enumerable.Range(0, 20)
            .Where(i => DistribuidorFaixaVencimento.ObterFaixa(i) == DistribuidorFaixaVencimento.Faixa.AVencerEmBreve);

        foreach (var indice in indices)
        {
            var data = DistribuidorFaixaVencimento.CalcularData(indice, referencia);
            var diasNoFuturo = (data - referencia).TotalDays;
            Assert.InRange(diasNoFuturo, 1, 30);
        }
    }

    [Fact]
    public void CalcularData_ParaFaixaValida_DeveRetornarDataFuturaAlemDeTrintaDias()
    {
        var referencia = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

        var indices = Enumerable.Range(0, 20)
            .Where(i => DistribuidorFaixaVencimento.ObterFaixa(i) == DistribuidorFaixaVencimento.Faixa.Valido);

        foreach (var indice in indices)
        {
            var data = DistribuidorFaixaVencimento.CalcularData(indice, referencia);
            var diasNoFuturo = (data - referencia).TotalDays;
            Assert.InRange(diasNoFuturo, 31, 365);
        }
    }

    [Fact]
    public void CalcularData_FaixaVencida_DeveProduziDiversidadeCompletuDe10Valores()
    {
        var referencia = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

        // Percorre 200 índices (10 blocos de 20) para cobrir variação completa
        var indicesVencidos = Enumerable.Range(0, 200)
            .Where(i => DistribuidorFaixaVencimento.ObterFaixa(i) == DistribuidorFaixaVencimento.Faixa.Vencido);

        var diasDistintos = indicesVencidos
            .Select(i => DistribuidorFaixaVencimento.CalcularData(i, referencia))
            .Select(d => (referencia - d).TotalDays)
            .Distinct()
            .ToList();

        // Esperamos pelo menos 9 valores distintos (cobrindo quase todo o range 5-59)
        Assert.True(diasDistintos.Count >= 9,
            $"Faixa Vencido deveria produzir pelo menos 9 valores distintos, mas produziu {diasDistintos.Count}: {string.Join(", ", diasDistintos.OrderBy(d => d))}");
    }

    [Fact]
    public void CalcularData_FaixaAVencerEmBreve_DeveProduziDiversidadeCompletuDe10Valores()
    {
        var referencia = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

        // Percorre 200 índices (10 blocos de 20) para cobrir variação completa
        var indices = Enumerable.Range(0, 200)
            .Where(i => DistribuidorFaixaVencimento.ObterFaixa(i) == DistribuidorFaixaVencimento.Faixa.AVencerEmBreve);

        var diasDistintos = indices
            .Select(i => DistribuidorFaixaVencimento.CalcularData(i, referencia))
            .Select(d => (d - referencia).TotalDays)
            .Distinct()
            .ToList();

        // Esperamos pelo menos 9 valores distintos (cobrindo quase todo o range 1-28)
        Assert.True(diasDistintos.Count >= 9,
            $"Faixa AVencerEmBreve deveria produzir pelo menos 9 valores distintos, mas produziu {diasDistintos.Count}: {string.Join(", ", diasDistintos.OrderBy(d => d))}");
    }
}
