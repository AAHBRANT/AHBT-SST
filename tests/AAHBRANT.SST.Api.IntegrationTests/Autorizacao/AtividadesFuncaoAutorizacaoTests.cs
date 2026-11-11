using System.Reflection;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

// Atividades por função na OS (10/11/2026): só o Engenheiro de Segurança decide; técnico não chega ao
// handler (a policy "perfil:engenheiro-seguranca" nega). Nenhum endpoint anônimo e sem DELETE.
public class AtividadesFuncaoAutorizacaoTests
{
    private static IEnumerable<MethodInfo> Acoes() =>
        typeof(AtividadesFuncaoController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    private static string Politica(string acao) =>
        typeof(AtividadesFuncaoController).GetMethod(acao)!.GetCustomAttributes<AuthorizeAttribute>().Single().Policy!;

    [Fact]
    public void DecidirExigeEngenheiroDeSeguranca()
        => Assert.Equal(PoliticasAutorizacao.SomenteEngenheiroSeguranca, Politica(nameof(AtividadesFuncaoController.Decidir)));

    [Fact]
    public void MatrizExigeRiscoVer_ESugerirExigeRiscoEditar()
    {
        Assert.Equal("risco:ver", Politica(nameof(AtividadesFuncaoController.ObterMatriz)));
        Assert.Equal("risco:editar", Politica(nameof(AtividadesFuncaoController.SugerirComIa)));
    }

    [Fact]
    public void NenhumEndpointAnonimoENenhumDelete()
    {
        Assert.Empty(typeof(AtividadesFuncaoController).GetCustomAttributes<AllowAnonymousAttribute>());
        foreach (var acao in Acoes())
        {
            Assert.Empty(acao.GetCustomAttributes<AllowAnonymousAttribute>());
            Assert.NotEmpty(acao.GetCustomAttributes<AuthorizeAttribute>());
            Assert.DoesNotContain(acao.GetCustomAttributes<HttpMethodAttribute>(), a => a.HttpMethods.Contains("DELETE"));
        }
    }
}
