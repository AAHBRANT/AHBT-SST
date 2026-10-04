using System.Net;
using System.Net.Http.Json;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.Plataforma;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Api.IntegrationTests.Plataforma;

public class PlataformaHttpTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    public async Task InitializeAsync() => _app = await PlataformaTestHost.IniciarAsync();
    public async Task DisposeAsync() => await _app.DisposeAsync();
    private HttpClient Cliente(string? oid = "operador")
    {
        var cliente = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
        if (oid != null) cliente.DefaultRequestHeaders.Add("X-Test-User", oid);
        return cliente;
    }

    [Fact] public async Task ListaSomenteObrasDoModulo()
    {
        using var cliente = Cliente();
        var obras = await cliente.GetFromJsonAsync<ObraModuloDto[]>("/api/plataforma/modulos/qualidade/obras");
        Assert.Equal(PlataformaTestHost.ObraA, Assert.Single(obras!).Id);
    }

    [Fact] public async Task ConsultaGlobalNaoAmpliaAcaoDeEdicao()
    {
        using var cliente = Cliente();
        var obras = await cliente.GetFromJsonAsync<Guid[]>("/test/editar/obras");
        Assert.Equal(PlataformaTestHost.ObraA, Assert.Single(obras!));
    }

    [Theory]
    [InlineData(null, 401)] [InlineData("desconhecido", 403)] [InlineData("administrador", 403)]
    public async Task SemConcessaoExplicitaNaoAcessaQualidade(string? oid, int esperado)
    {
        using var cliente = Cliente(oid);
        Assert.Equal(esperado, (int)(await cliente.GetAsync("/api/plataforma/modulos/qualidade/obras")).StatusCode);
    }

    [Fact] public async Task AlterarObraNaUrlNaoAmpliaAcesso()
    {
        using var cliente = Cliente();
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/plataforma/modulos/qualidade/obras/{PlataformaTestHost.ObraB}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/plataforma/abas/qualidade/{PlataformaTestHost.ObraB}")).StatusCode);
    }

    [Theory] [InlineData("separado")] [InlineData("somente-leitura")]
    public async Task ConfiguracaoExigeAmbasPermissoesNaMesmaObra(string oid)
    {
        using var cliente = Cliente(oid);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/plataforma/modulos/qualidade/obras?configurarTeams=true")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/plataforma/abas/qualidade/{PlataformaTestHost.ObraA}")).StatusCode);
    }

    [Fact] public async Task ConfiguracaoAutorizadaRetornaRotaDaObra()
    {
        using var cliente = Cliente();
        var texto = await cliente.GetStringAsync($"/api/plataforma/abas/qualidade/{PlataformaTestHost.ObraA}");
        Assert.Contains($"/qualidade?obraId={PlataformaTestHost.ObraA}", texto);
    }

    [Fact] public async Task ConcessaoGlobalPermiteDuasObras()
    {
        using var cliente = Cliente("global");
        Assert.Equal(2, (await cliente.GetFromJsonAsync<ObraModuloDto[]>("/api/plataforma/modulos/qualidade/obras?configurarTeams=true"))!.Length);
    }

    [Theory] [InlineData("vinculo")] [InlineData("perfil")] [InlineData("usuario")]
    [InlineData("status")] [InlineData("permissao")] [InlineData("matriz")] [InlineData("negada")]
    public async Task RevogacaoImpedeProximaRequisicao(string alvo)
    {
        using var cliente = Cliente();
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/plataforma/abas/qualidade/{PlataformaTestHost.ObraA}")).StatusCode);
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
            var v = await db.UsuariosPerfilObra.Include(v => v.Usuario).Include(v => v.PerfilAcesso!).ThenInclude(p => p.Permissoes).ThenInclude(p => p.Permissao)
                .SingleAsync(v => v.Usuario!.AzureAdObjectId == "operador" && v.ObraId == PlataformaTestHost.ObraA);
            var p = v.PerfilAcesso!.Permissoes.Single(p => p.Permissao!.Codigo == "qualidade:ver");
            switch (alvo)
            {
                case "vinculo": v.Ativo = false; break;
                case "perfil": v.PerfilAcesso.Ativo = false; break;
                case "usuario": v.Usuario!.Ativo = false; break;
                case "status": v.Usuario!.Status = StatusUsuario.Bloqueado; break;
                case "permissao": p.Permissao!.Ativo = false; break;
                case "matriz": p.Ativo = false; break;
                case "negada": p.Permitido = false; break;
            }
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/plataforma/abas/qualidade/{PlataformaTestHost.ObraA}")).StatusCode);
    }

    [Fact] public void ProducaoSemEntraNaoInicia()
    {
        // Fonte em memória (vazia): sem nenhuma fonte registrada, o indexador de IConfigurationRoot
        // lança "A configuration source is not registered" ao gravar um valor.
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        Assert.Throws<InvalidOperationException>(() => ConfiguracaoAutenticacao.Validar(config, false));
        ConfiguracaoAutenticacao.Validar(config, true);
        config["AzureAd:TenantId"] = "tenant";
        Assert.Throws<InvalidOperationException>(() => ConfiguracaoAutenticacao.Validar(config, false));
        config["AzureAd:ClientId"] = "client";
        ConfiguracaoAutenticacao.Validar(config, false);
    }
}
