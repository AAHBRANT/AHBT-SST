using AAHBRANT.SST.Application.Cbo;
using AAHBRANT.SST.Infrastructure.Persistencia.Seed;

namespace AAHBRANT.SST.Infrastructure.Tests.Cbo;

// Arquivos oficiais da CBO 2002 embutidos no app (baixados do portal cbo.mte.gov.br em 29/10/2026).
public class TabelaCboSeederTests
{
    [Fact]
    public void LerTitulos_TrazTodasAsOcupacoesESinonimosDoArquivoOficial()
    {
        var titulos = TabelaCboSeeder.LerTitulos();

        Assert.Equal(2725, titulos.Count(t => t.Oficial));
        Assert.Equal(7870, titulos.Count(t => !t.Oficial));
        Assert.All(titulos, t => Assert.Matches("^[0-9]{6}$", t.Codigo));
        Assert.Contains(titulos, t => t is { Codigo: "717020", Titulo: "Servente de obras", Oficial: true });
    }

    [Fact]
    public void CatalogoComATabelaReal_AchaFuncoesDeObra()
    {
        var catalogo = new CatalogoCbo(TabelaCboSeeder.LerTitulos().Select(t => (t.Codigo, t.Titulo, t.Oficial)));

        Assert.Contains(catalogo.Buscar("rasteleiro"), r => r.Codigo == "715205");
        Assert.Contains(catalogo.Candidatos("Greidista / Nivelador"), r => r.Codigo == "312320");
        Assert.Contains(catalogo.Candidatos("Servente"), r => r.Codigo == "717020");
        Assert.Equal("Pedreiro", catalogo.TituloOficial("7152-10"));
    }
}
