using System.Security.Claims;
using System.Text.Encodings.Web;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Controllers;
using AAHBRANT.SST.Api.Middlewares;
using AAHBRANT.SST.Application;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Api.IntegrationTests.Plataforma;

// Host isolado: usa controllers, políticas, middleware, handlers e DbContext reais.
// A identidade sintética e o banco em memória existem SOMENTE no projeto de testes.
public static class PlataformaTestHost
{
    public static readonly Guid ObraA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ObraB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static async Task<WebApplication> IniciarAsync(string url = "http://127.0.0.1:0", string? usuarioNavegador = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls(url);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Configuration["AzureAd:TenantId"] = "tenant-de-teste";
        builder.Services.AddSingleton(new IdentidadeNavegador(usuarioNavegador));
        builder.Services.AddAuthentication("Teste").AddScheme<AuthenticationSchemeOptions, AutenticacaoTeste>("Teste", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissaoAuthorizationPolicyProvider>();
        builder.Services.AddScoped<IAuthorizationHandler, PermissaoAuthorizationHandler>();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        var banco = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<SstDbContext>(o => o.UseInMemoryDatabase(banco));
        builder.Services.AddScoped<IAppDbContext>(s => s.GetRequiredService<SstDbContext>());
        // Só os handlers necessários; não registra integrações externas ou jobs.
        builder.Services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        builder.Services.AddScoped<AAHBRANT.SST.Application.Common.Seguranca.IAcessoPorObraService, AAHBRANT.SST.Application.Common.Seguranca.AcessoPorObraService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(PlataformaController).Assembly);
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.SetIsOriginAllowed(o => new Uri(o).Host == "localhost").AllowAnyHeader().AllowAnyMethod()));
        var app = builder.Build();
        app.UseMiddleware<TratamentoDeExcecaoMiddleware>();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<EscopoPorObraMiddleware>();
        app.MapControllers();
        app.MapGet("/test/editar/obras", async (SstDbContext db) => await db.Obras.Select(o => o.Id).ToListAsync())
            .RequireAuthorization("obra:editar");

        using (var scope = app.Services.CreateScope())
        {
            CpfCriptografiaContexto.Configurar(Enumerable.Repeat((byte)1, 32).ToArray(), Enumerable.Repeat((byte)2, 32).ToArray());
            var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();
            db.Obras.AddRange(new Obra { Id = ObraA, Codigo = "QA-001", Nome = "Obra de teste · Alfa", Cidade = "São Paulo", Uf = "SP" },
                new Obra { Id = ObraB, Codigo = "QA-002", Nome = "Obra de teste · Beta" });
            Conceder(db, "operador", ObraA, "qualidade:ver", "teams:configurar", "obra:editar", "organizacional:ver");
            Conceder(db, "operador", ObraB, "organizacional:ver");
            Conceder(db, "operador", null, "calendario:ver");
            Conceder(db, "separado", ObraA, "qualidade:ver");
            Conceder(db, "separado", ObraB, "teams:configurar");
            Conceder(db, "global", null, "qualidade:ver", "teams:configurar");
            Conceder(db, "somente-leitura", ObraA, "qualidade:ver");
            Conceder(db, "administrador", null, "usuario:ver").PerfilAcesso!.Tipo = AAHBRANT.SST.Domain.Enums.TipoPerfilAcesso.Administrador;
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        return app;
    }

    public static UsuarioPerfilObra Conceder(SstDbContext db, string oid, Guid? obra, params string[] codigos)
    {
        var usuario = db.Usuarios.Local.FirstOrDefault(u => u.AzureAdObjectId == oid) ?? new Usuario { Nome = oid, AzureAdObjectId = oid, Email = $"{oid}@teste.invalid" };
        var perfil = new PerfilAcesso { Nome = $"Teste-{Guid.NewGuid()}" };
        foreach (var codigo in codigos)
            perfil.Permissoes.Add(new PerfilAcessoPermissao { Permissao = new Permissao { Codigo = codigo, Modulo = "Teste", Acao = "Ver" } });
        var vinculo = new UsuarioPerfilObra { Usuario = usuario, PerfilAcesso = perfil, ObraId = obra };
        db.UsuariosPerfilObra.Add(vinculo);
        return vinculo;
    }

    private sealed record IdentidadeNavegador(string? Oid);
    private sealed class AutenticacaoTeste(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, IdentidadeNavegador navegador) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var oid = Request.Headers["X-Test-User"].FirstOrDefault() ?? navegador.Oid;
            if (oid is null) return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", oid)], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
