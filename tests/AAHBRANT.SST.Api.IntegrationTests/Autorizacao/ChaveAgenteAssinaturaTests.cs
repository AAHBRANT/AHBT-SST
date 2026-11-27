using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Controllers;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

// Rotas internas do agente engenheiro da assinatura: desligadas sem chave, 401 com chave errada e acesso a
// todas as obras só com a chave certa.
public class ChaveAgenteAssinaturaTests
{
    private const string Chave = "chave-de-teste-com-mais-de-32-caracteres!!";

    private static (AuthorizationFilterContext Contexto, CurrentUserService Usuario) Contexto(string? chaveConfigurada, string? chaveEnviada)
    {
        var usuario = new CurrentUserService();
        usuario.DefinirEscopo(temAcessoGlobal: false, Array.Empty<Guid>());
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ChaveAgenteAssinaturaAttribute.Configuracao] = chaveConfigurada })
            .Build();
        var servicos = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuracao)
            .AddSingleton<ICurrentUserService>(usuario)
            .BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = servicos };
        if (chaveEnviada is not null)
            http.Request.Headers[ChaveAgenteAssinaturaAttribute.Cabecalho] = chaveEnviada;
        var acao = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return (new AuthorizationFilterContext(acao, new List<IFilterMetadata>()), usuario);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("curta-demais")]
    public async Task SemChaveConfigurada_RotaFicaDesligada(string? configurada)
    {
        var (contexto, usuario) = Contexto(configurada, configurada);

        await new ChaveAgenteAssinaturaAttribute().OnAuthorizationAsync(contexto);

        Assert.IsType<NotFoundResult>(contexto.Result);
        Assert.False(usuario.TemAcessoGlobal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("chave-de-teste-com-mais-de-32-caracteres!?")]
    public async Task ChaveErrada_Da401_ENaoLiberaAsObras(string? enviada)
    {
        var (contexto, usuario) = Contexto(Chave, enviada);

        await new ChaveAgenteAssinaturaAttribute().OnAuthorizationAsync(contexto);

        Assert.IsType<UnauthorizedResult>(contexto.Result);
        Assert.False(usuario.TemAcessoGlobal);
    }

    [Fact]
    public async Task ChaveCerta_LiberaTodasAsObras()
    {
        var (contexto, usuario) = Contexto(Chave, Chave);

        await new ChaveAgenteAssinaturaAttribute().OnAuthorizationAsync(contexto);

        Assert.Null(contexto.Result);
        Assert.True(usuario.TemAcessoGlobal);
    }

    [Fact]
    public void Controller_ExigeAChave_ELimitaRequisicoes()
    {
        var tipo = typeof(AgenteAssinaturaController);
        Assert.NotNull(tipo.GetCustomAttributes(typeof(ChaveAgenteAssinaturaAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(tipo.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(tipo.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), inherit: true).SingleOrDefault());
    }
}
