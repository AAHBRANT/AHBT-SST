using System.Reflection;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Controllers;
using AAHBRANT.SST.Api.Middlewares;
using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

// Rota anônima do agente de câmeras. Lição já paga neste projeto: com o Entra ligado (produção/hml) o
// EscopoPorObraMiddleware deixa o escopo ZERADO numa rota anônima e os filtros globais escondem tudo; em dev
// (Entra desligado, escopo global) o bug não aparece. Aqui o middleware REAL roda com AzureAd:TenantId preenchido.
public class AgentesCameraRotaAnonimaTests
{
    private sealed class SenderQueExigeACameraVisivel : ISender
    {
        private readonly SstDbContext _db;
        public bool CameraVisivel { get; private set; }
        public SenderQueExigeACameraVisivel(SstDbContext db) => _db = db;

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var comando = Assert.IsType<AnalisarQuadroCameraCommand>(request);
            var camera = await _db.Cameras.FirstOrDefaultAsync(c => c.Id == comando.CameraId, cancellationToken)
                ?? throw new KeyNotFoundException("Câmera não encontrada (escopo zerado escondeu a câmera).");
            CameraVisivel = true;
            // Gravação sob o mesmo escopo, como a análise real faz.
            _db.OcorrenciasEpiCamera.Add(new OcorrenciaEpiCamera
            {
                CameraId = camera.Id, ObraId = camera.ObraId, Foto = comando.Imagem, CapturadaEmUtc = comando.CapturadaEmUtc,
                RecebidaEmUtc = DateTime.UtcNow, PessoasNaCena = 1, SemCapacete = 1,
            });
            await _db.SaveChangesAsync(cancellationToken);
            return (TResponse)(object)new ResultadoQuadroDto(SituacaoQuadro.Ocorrencia, null, false, 1, 1, 0);
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task EntraLigado_RotaAnonima_MiddlewareZeraOEscopo_EHandlerDoAgenteRestauraSoAObraDele()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nameof(EntraLigado_RotaAnonima_MiddlewareZeraOEscopo_EHandlerDoAgenteRestauraSoAObraDele)).Options;
        var hasher = new SegredoDispositivoHasherService();
        var global = new SstDbContext(opcoes, new CurrentUserService());
        var obraA = new Obra { Codigo = "OA", Nome = "Obra A" };
        var obraB = new Obra { Codigo = "OB", Nome = "Obra B" };
        var cameraA = new Camera { ObraId = obraA.Id, Nome = "Betoneira" };
        var cameraB = new Camera { ObraId = obraB.Id, Nome = "Portaria B" };
        var segredo = hasher.GerarSegredo();
        var agente = new AgenteCamera { ObraId = obraA.Id, Nome = "PC A", SegredoHash = hasher.GerarHash(segredo) };
        global.AddRange(obraA, obraB, cameraA, cameraB, agente);
        await global.SaveChangesAsync();

        // Requisição anônima, Entra ligado (TenantId preenchido), exatamente como chega do agente.
        var escopo = new CurrentUserService();
        var db = new SstDbContext(opcoes, escopo);
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AzureAd:TenantId"] = "tenant-qualquer" }).Build();
        var contexto = new DefaultHttpContext();
        var sender = new SenderQueExigeACameraVisivel(db);
        var handler = new AnalisarQuadroAgenteCameraCommandHandler(db, new AgenteCameraAutenticador(db, hasher), escopo, sender,
            NullLogger<AnalisarQuadroAgenteCameraCommandHandler>.Instance);

        ResultadoQuadroDto? resultado = null;
        async Task Chamar(Guid cameraId)
        {
            resultado = await handler.Handle(new AnalisarQuadroAgenteCameraCommand(
                agente.Id, segredo, "1.0.0", cameraId, new byte[] { 0xFF, 0xD8, 1 }, DateTime.UtcNow), default);
        }

        var middleware = new EscopoPorObraMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(contexto, db, escopo, configuracao, acesso: null!);

        // Prova do problema: o middleware deixou escopo zerado e os filtros escondem a câmera da própria obra.
        Assert.False(escopo.TemAcessoGlobal);
        Assert.Empty(escopo.ObrasPermitidas);
        Assert.Empty(await db.Cameras.ToListAsync());

        // O handler valida o segredo e só então define o escopo da obra do agente.
        await Chamar(cameraA.Id);

        Assert.NotNull(resultado);
        Assert.True(sender.CameraVisivel);
        Assert.Equal(new[] { obraA.Id }, escopo.ObrasPermitidas);
        Assert.False(escopo.TemAcessoGlobal);
        var ocorrencia = await global.OcorrenciasEpiCamera.AsNoTracking().SingleAsync();
        Assert.Equal(obraA.Id, ocorrencia.ObraId);

        // A câmera de outra obra continua invisível (404), mesmo com o segredo certo.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Chamar(cameraB.Id));
    }

    [Fact]
    public void EnviarQuadro_EhAnonimo_ComLimitePorIp_ESemPolicyDoPiloto()
    {
        var action = typeof(AgentesCameraController).GetMethod(nameof(AgentesCameraController.EnviarQuadro))!;

        Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Empty(action.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Empty(typeof(AgentesCameraController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("agente-camera-ip", action.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        var rota = action.GetCustomAttribute<HttpPostAttribute>()!.Template;
        Assert.Equal("{agenteId:guid}/cameras/{cameraId:guid}/quadros", rota);
        Assert.Equal("api/agentes-camera", typeof(AgentesCameraController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Fact]
    public void EnviarQuadro_NaoAceitaSegredoNaQueryString()
    {
        var action = typeof(AgentesCameraController).GetMethod(nameof(AgentesCameraController.EnviarQuadro))!;
        Assert.DoesNotContain(action.GetParameters(), p => p.Name!.Contains("segredo", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(action.GetParameters(), p => p.GetCustomAttribute<FromQueryAttribute>() is not null);
        Assert.Equal("X-Agente-Segredo", AgentesCameraController.CabecalhoSegredo);
    }

    // A exclusão é só do Administrador: a policy do piloto já é Administrador + lista nomeada, e o teste estrutural de
    // CamerasPilotoPoliticaTests exige que TODA action do CamerasController (inclusive as de agentes) a tenha.
    [Fact]
    public void GestaoDeAgentes_ExigeOPilotoDeCameras()
    {
        foreach (var nome in new[] { nameof(CamerasController.CriarAgente), nameof(CamerasController.ListarAgentes), nameof(CamerasController.RevogarAgente) })
        {
            var action = typeof(CamerasController).GetMethod(nome)!;
            Assert.Equal(new[] { PoliticasAutorizacao.CamerasPiloto }, action.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy));
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
        }
        Assert.Equal("agentes/{id:guid}", typeof(CamerasController).GetMethod(nameof(CamerasController.RevogarAgente))!
            .GetCustomAttribute<HttpDeleteAttribute>()!.Template);
    }
}
