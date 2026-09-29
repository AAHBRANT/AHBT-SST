using AAHBRANT.SST.AgenteBiometria.Endpoints;
using Microsoft.AspNetCore.Http;

namespace AAHBRANT.SST.AgenteBiometria.Tests.Endpoints;

public class AcessoRedePrivadaTests
{
    private const string Origem = "https://sst-web-hml.example.com";

    private static HttpRequest Requisicao(string metodo, string? origem, string? pedidoRedePrivada)
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Method = metodo;
        if (origem is not null) contexto.Request.Headers.Origin = origem;
        if (pedidoRedePrivada is not null) contexto.Request.Headers[AcessoRedePrivada.CabecalhoPedido] = pedidoRedePrivada;
        return contexto.Request;
    }

    [Fact]
    public void Preflight_DaOrigemPermitidaPedindoRedePrivada_Libera()
    {
        Assert.True(AcessoRedePrivada.DeveLiberar(Requisicao("OPTIONS", Origem, "true"), Origem));
    }

    [Fact]
    public void OrigemConfiguradaComBarraFinal_TambemLibera()
    {
        Assert.True(AcessoRedePrivada.DeveLiberar(Requisicao("OPTIONS", Origem, "true"), Origem + "/"));
    }

    [Fact]
    public void OutraOrigem_NaoLibera()
    {
        Assert.False(AcessoRedePrivada.DeveLiberar(Requisicao("OPTIONS", "https://site-malicioso.example", "true"), Origem));
    }

    [Fact]
    public void RequisicaoQueNaoEPreflight_NaoLibera()
    {
        Assert.False(AcessoRedePrivada.DeveLiberar(Requisicao("POST", Origem, "true"), Origem));
    }

    [Fact]
    public void PreflightSemPedidoDeRedePrivada_NaoLibera()
    {
        Assert.False(AcessoRedePrivada.DeveLiberar(Requisicao("OPTIONS", Origem, null), Origem));
    }

    [Fact]
    public async Task Middleware_AdicionaOCabecalhoNaResposta()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Method = "OPTIONS";
        contexto.Request.Headers.Origin = Origem;
        contexto.Request.Headers[AcessoRedePrivada.CabecalhoPedido] = "true";

        var app = new Microsoft.AspNetCore.Builder.ApplicationBuilder(new EmptyServiceProvider())
            .UseAcessoRedePrivada(Origem)
            .Build();
        await app(contexto);

        Assert.Equal("true", contexto.Response.Headers[AcessoRedePrivada.CabecalhoResposta].ToString());
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
