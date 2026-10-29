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

    // Funções reais do rascunho do Beira Rio que ficavam sem CBO porque o código certo não entrava nos candidatos.
    [Theory]
    [InlineData("Motorista Operador de Munck", "782515")]
    [InlineData("Encarregado de Pavimentação / Pleno", "710205")]
    [InlineData("Encarregado de Obras", "710205")]
    [InlineData("Eletricista", "715615")]
    [InlineData("Operador de Retroescavadeira", "715115")]
    [InlineData("Operador de Rolo Compactador", "715110")]
    public void Candidatos_FuncoesDeObra_TrazemOCodigoCertoEntreOsPrimeiros(string funcao, string codigo)
    {
        var catalogo = new CatalogoCbo(TabelaCboSeeder.LerTitulos().Select(t => (t.Codigo, t.Titulo, t.Oficial)));

        Assert.Contains(catalogo.Candidatos(funcao).Take(6), r => r.Codigo == codigo);
    }

    // O CBO é da atividade: com a descrição, entram os códigos que o nome sozinho não traz (rascunho do Beira Rio, 29/10).
    [Theory]
    [InlineData("Motorista", "Dirige veículos leves da obra para transporte de pessoas, documentos e materiais.", "782305")]
    [InlineData("Eletricista", "Instala e liga cabos, eletrodutos e quadros da rede elétrica de distribuição.", "732120")]
    [InlineData("Auxiliar de Mecânico", "Auxilia o mecânico na troca de peças e na lubrificação de máquinas e veículos.", "914405")]
    [InlineData("Apontador", "Registra presença, horas e produção dos trabalhadores.", "414205")]
    public void Candidatos_ComDescricao_TrazemOCodigoDaAtividade(string funcao, string descricao, string codigo)
    {
        var catalogo = new CatalogoCbo(TabelaCboSeeder.LerTitulos().Select(t => (t.Codigo, t.Titulo, t.Oficial)));

        Assert.Contains(catalogo.Candidatos(funcao, descricao), r => r.Codigo == codigo);
    }
}
