using AAHBRANT.SST.Infrastructure.Persistencia.Seed;

namespace AAHBRANT.SST.Api.IntegrationTests.Persistencia.Seed;

public class MockObraSeederDadosEstaticosTests
{
    [Fact]
    public void DistribuicaoFuncoes_DeveSomarDuzentosTrabalhadores()
    {
        var total = MockObraSeeder.DistribuicaoFuncoes.Sum(f => f.Quantidade);

        Assert.Equal(200, total);
    }

    [Fact]
    public void DistribuicaoNaoConformidades_DeveSomarVinteECincoRegistros()
    {
        var total = MockObraSeeder.DistribuicaoNaoConformidades.Sum(n => n.Quantidade);

        Assert.Equal(25, total);
    }

    [Fact]
    public void CatalogoEpisPadrao_DeveTerAoMenosDoisItensComSaldoCritico()
    {
        var itensCriticos = MockObraSeeder.CatalogoEpisPadrao.Count(e => e.SaldoEstoque <= 3);

        Assert.True(itensCriticos >= 2, $"Esperado >= 2 itens com saldo <= 3, encontrado {itensCriticos}");
    }

    [Fact]
    public void CatalogoCursosNr_TodosOsCodigosUsadosEmDistribuicaoFuncoesDevemExistirNoCatalogo()
    {
        var codigosDoCatalogo = MockObraSeeder.CatalogoCursosNr.Select(c => c.Codigo).ToHashSet();
        var codigosUsados = MockObraSeeder.DistribuicaoFuncoes.SelectMany(f => f.CodigosCursos).Distinct();

        foreach (var codigo in codigosUsados)
            Assert.Contains(codigo, codigosDoCatalogo);
    }

    [Fact]
    public void GerarNome_ParaDuzentosIndices_DeveTerBaixaTaxaDeColisao()
    {
        var nomes = Enumerable.Range(0, 200).Select(MockObraSeeder.GerarNome).ToList();

        Assert.True(nomes.Distinct().Count() >= 150, "Esperado ao menos 150 nomes distintos em 200 gerados");
    }

    [Fact]
    public void DistribuicaoFuncoes_QuantidadeAntesDeEncarregado_DeveSerMultiploDeDezEquipes()
    {
        // A distribuição de trabalhadores por equipe (10 equipes) depende de a soma das
        // quantidades das funções anteriores a "Encarregado" na tabela ser múltiplo de 10 —
        // caso contrário, alguma(s) equipe(s) ficam sem Encarregado sem que nenhum erro apareça.
        var somaAntes = MockObraSeeder.DistribuicaoFuncoes
            .TakeWhile(f => f.Funcao != MockObraSeeder.FuncaoEncarregado)
            .Sum(f => f.Quantidade);

        Assert.Equal(0, somaAntes % 10);
    }
}
