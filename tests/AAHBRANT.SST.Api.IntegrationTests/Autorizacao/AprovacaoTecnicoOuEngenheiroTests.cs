using System.Reflection;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

// Decisão do usuário (17/11/2026): técnico, engenheiro OU administrador aprovam cursos obrigatórios e
// atividades por função; a nova versão da matriz de risco é de técnico ou engenheiro (sem administrador);
// o PGR (aprovar e devolver) segue só do engenheiro.
public class AprovacaoTecnicoOuEngenheiroTests
{
    private static string Politica(Type controller, string acao) =>
        controller.GetMethod(acao)!.GetCustomAttributes<AuthorizeAttribute>().Single().Policy!;

    [Theory]
    [InlineData(nameof(CursosObrigatoriosController.AprovarSugestoes))]
    [InlineData(nameof(CursosObrigatoriosController.Adicionar))]
    [InlineData(nameof(CursosObrigatoriosController.Decidir))]
    public void CursosObrigatorios_AprovarAdicionarEDecidir_SaoDeTecnicoEngenheiroOuAdministrador(string acao)
        => Assert.Equal(PoliticasAutorizacao.DecisorDeSugestoes, Politica(typeof(CursosObrigatoriosController), acao));

    [Fact]
    public void AprovarEDevolverOPgr_ContinuamSoDoEngenheiro()
    {
        Assert.Equal(PoliticasAutorizacao.SomenteEngenheiroSeguranca, Politica(typeof(GeracoesPgrEapController), nameof(GeracoesPgrEapController.Aprovar)));
        Assert.Equal(PoliticasAutorizacao.SomenteEngenheiroSeguranca, Politica(typeof(GeracoesPgrEapController), nameof(GeracoesPgrEapController.Devolver)));
    }

    [Fact]
    public void NovaVersaoDaMatrizDeRisco_EDeTecnicoOuEngenheiro_SemAdministrador()
    {
        var politica = Politica(typeof(MatrizRiscoController), nameof(MatrizRiscoController.NovaVersao));
        Assert.Equal(PoliticasAutorizacao.TecnicoOuEngenheiroSeguranca, politica);
        // A policy dos cursos/atividades (que inclui Administrador) nao pode ser a da matriz.
        Assert.NotEqual(PoliticasAutorizacao.DecisorDeSugestoes, politica);
    }

    [Fact]
    public void CursosEAtividades_NaoUsamAPolicyDaMatrizNemADoPgr()
    {
        Assert.NotEqual(PoliticasAutorizacao.DecisorDeSugestoes, PoliticasAutorizacao.TecnicoOuEngenheiroSeguranca);
        Assert.NotEqual(PoliticasAutorizacao.DecisorDeSugestoes, PoliticasAutorizacao.SomenteEngenheiroSeguranca);
    }

    [Fact]
    public void PoliticaNovaTemCodigoProprio_NaoColideComADoEngenheiro()
        => Assert.NotEqual(PoliticasAutorizacao.SomenteEngenheiroSeguranca, PoliticasAutorizacao.TecnicoOuEngenheiroSeguranca);
}
