using System.Reflection;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Controllers;
using AAHBRANT.SST.Api.Middlewares;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

// Exclusão definitiva de turma (10/11/2026): os três endpoints são só do Administrador (a policy devolve 403 a qualquer outro
// perfil, pelo PermissaoAuthorizationHandler) e nenhum é anônimo. Teste estrutural, no mesmo estilo de CamerasPilotoPoliticaTests.
public class ExclusaoTurmaSomenteAdministradorTests
{
    private static MethodInfo Acao(string nome) =>
        typeof(SessoesTreinamentoController).GetMethod(nome, BindingFlags.Public | BindingFlags.Instance)!;

    [Theory]
    [InlineData(nameof(SessoesTreinamentoController.PreviaExclusao), typeof(HttpGetAttribute), "{id:guid}/previa-exclusao")]
    [InlineData(nameof(SessoesTreinamentoController.Excluir), typeof(HttpDeleteAttribute), "{id:guid}")]
    [InlineData(nameof(SessoesTreinamentoController.ExcluirLote), typeof(HttpPostAttribute), "excluir-lote")]
    public void EndpointsDeExclusaoSaoSomenteAdministrador(string nome, Type verbo, string rota)
    {
        var acao = Acao(nome);

        var autorizacoes = acao.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).ToList();
        Assert.True(autorizacoes.SequenceEqual([PoliticasAutorizacao.SomenteAdministrador]),
            $"{nome} deveria ter só a policy {PoliticasAutorizacao.SomenteAdministrador}.");
        Assert.Empty(acao.GetCustomAttributes<AllowAnonymousAttribute>());

        var http = (HttpMethodAttribute)acao.GetCustomAttributes(verbo).Single();
        Assert.Equal(rota, http.Template);
    }

    [Fact]
    public void ControllerNaoLiberaAnonimoNemMudaOEscopoDaPolicyDeAdministrador()
    {
        Assert.Empty(typeof(SessoesTreinamentoController).GetCustomAttributes<AllowAnonymousAttribute>());
        // Administrador restrito a obras continua preso ao filtro de obras da turma (leitura filtrada = 404 fora do escopo).
        Assert.False(EscopoPorObraMiddleware.RestringeEscopo(PoliticasAutorizacao.SomenteAdministrador));
    }
}
