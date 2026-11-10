using System.Reflection;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Controllers;
using AAHBRANT.SST.Api.Middlewares;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

// Piloto do módulo Câmeras (09/11/2026): só Administrador cujo e-mail está em Cameras:UsuariosPiloto.
// A decisão é uma função pura (o handler resolve o Administrador no banco e o e-mail no token).
public class CamerasPilotoPoliticaTests
{
    private static readonly string[] Lista = ["wellington.lourenco@aahbrant.com"];

    [Fact]
    public void AdministradorNaListaEPermitido()
        => Assert.True(PermissaoAuthorizationHandler.PilotoCamerasPermitido(true, "wellington.lourenco@aahbrant.com", Lista));

    [Fact]
    public void AdministradorForaDaListaEhNegado()
        => Assert.False(PermissaoAuthorizationHandler.PilotoCamerasPermitido(true, "outro.admin@aahbrant.com", Lista));

    [Fact]
    public void NaListaMasNaoAdministradorEhNegado()
        => Assert.False(PermissaoAuthorizationHandler.PilotoCamerasPermitido(false, "wellington.lourenco@aahbrant.com", Lista));

    [Fact]
    public void ListaVaziaOuAusenteNegaTodos()
    {
        Assert.False(PermissaoAuthorizationHandler.PilotoCamerasPermitido(true, "wellington.lourenco@aahbrant.com", []));
        Assert.False(PermissaoAuthorizationHandler.PilotoCamerasPermitido(true, "wellington.lourenco@aahbrant.com", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SemEmailEhNegado(string? email)
        => Assert.False(PermissaoAuthorizationHandler.PilotoCamerasPermitido(true, email, [""," ", "wellington.lourenco@aahbrant.com"]));

    [Fact]
    public void EntradasEmBrancoNaListaNaoLiberamNinguem()
        => Assert.False(PermissaoAuthorizationHandler.PilotoCamerasPermitido(true, "x@aahbrant.com", ["", "  "]));

    [Theory]
    [InlineData("Wellington.Lourenco@AAHBRANT.com")]
    [InlineData("  wellington.lourenco@aahbrant.com  ")]
    public void ComparacaoIgnoraCaixaEEspacos(string email)
        => Assert.True(PermissaoAuthorizationHandler.PilotoCamerasPermitido(true, email, Lista));

    [Fact]
    public void EspacosNaListaConfiguradaTambemSaoIgnorados()
        => Assert.True(PermissaoAuthorizationHandler.PilotoCamerasPermitido(true, "wellington.lourenco@aahbrant.com", ["  Wellington.Lourenco@aahbrant.com "]));

    [Fact]
    public void PoliticaPilotoNaoRestringeAsObras()
        => Assert.False(EscopoPorObraMiddleware.RestringeEscopo(PoliticasAutorizacao.CamerasPiloto));

    // Falha fechada: toda action do controller exige o piloto; a única exceção é /acesso (menu).
    [Fact]
    public void TodaActionDeCamerasExigeOPilotoExcetoAcesso()
    {
        var actions = typeof(CamerasController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any())
            .ToList();
        Assert.NotEmpty(actions);

        foreach (var action in actions)
        {
            var politicas = action.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).ToList();
            var esperada = action.Name == nameof(CamerasController.Acesso)
                ? PoliticasAutorizacao.QualquerUsuarioAutenticado
                : PoliticasAutorizacao.CamerasPiloto;
            Assert.True(politicas.SequenceEqual([esperada]), $"{action.Name} deveria ter só a policy {esperada}.");
        }
        Assert.Empty(typeof(CamerasController).GetCustomAttributes<AuthorizeAttribute>());
    }
}
