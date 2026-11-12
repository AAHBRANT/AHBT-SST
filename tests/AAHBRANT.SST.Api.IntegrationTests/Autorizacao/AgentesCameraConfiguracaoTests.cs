using System.Reflection;
using System.Text;
using System.Text.Json;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Controllers;
using AAHBRANT.SST.Api.Middlewares;
using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

// Rotas anônimas do agente de câmeras introduzidas na etapa 3: configuração (com a senha das câmeras) e resultado do teste de
// conexão. Mesmo contrato da rota de quadros: autenticar antes de qualquer coisa, 401 indistinguível, limite por agente só
// para quem se autentica, e escopo da obra só depois do segredo (com o Entra ligado, como em produção/hml).
public class AgentesCameraConfiguracaoTests
{
    private const string Segredo = "segredo-certo";

    private sealed class AutenticadorFalso : IAgenteCameraAutenticador
    {
        public AgenteCamera Agente { get; } = new() { ObraId = Guid.NewGuid(), Nome = "PC" };

        public Task<AgenteCamera?> ValidarAsync(Guid agenteId, string? segredo, CancellationToken ct)
            => Task.FromResult<AgenteCamera?>(agenteId == Agente.Id && segredo == Segredo ? Agente : null);
    }

    private sealed class MediatorFalso : IMediator
    {
        public Func<object, object?> Resposta { get; set; } = _ => null;
        public List<object> Comandos { get; } = new();

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Comandos.Add(request);
            var resposta = Resposta(request);
            if (resposta is Exception ex) throw ex;
            return Task.FromResult((TResponse)resposta!);
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }

    private sealed class CorpoQueNaoPodeSerLido : MemoryStream
    {
        public bool FoiLido { get; private set; }
        public override int Read(byte[] buffer, int offset, int count) { FoiLido = true; throw new InvalidOperationException("corpo lido"); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { FoiLido = true; throw new InvalidOperationException("corpo lido"); }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { FoiLido = true; throw new InvalidOperationException("corpo lido"); }
    }

    private sealed record Montagem(AgentesCameraController Controller, AutenticadorFalso Autenticador, MediatorFalso Mediator, HttpContext Http, CorpoQueNaoPodeSerLido? Corpo);

    private static Montagem Montar(string? segredo = Segredo, string? corpoJson = null, int limitePorMinuto = LimitadorAgenteCamera.LimitePadraoPorMinuto)
    {
        var autenticador = new AutenticadorFalso();
        var mediator = new MediatorFalso();
        var controller = new AgentesCameraController(mediator, autenticador, new LimitadorAgenteCamera(limitePorMinuto), NullLogger<AgentesCameraController>.Instance);
        var http = new DefaultHttpContext();
        CorpoQueNaoPodeSerLido? sentinela = null;
        if (corpoJson is null)
        {
            sentinela = new CorpoQueNaoPodeSerLido();
            http.Request.Body = sentinela;
        }
        else
        {
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(corpoJson));
        }
        http.Request.ContentType = "application/json";
        if (segredo is not null) http.Request.Headers[AgentesCameraController.CabecalhoSegredo] = segredo;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return new Montagem(controller, autenticador, mediator, http, sentinela);
    }

    private static string Json(object? valor) => JsonSerializer.Serialize(valor);

    private static ConfiguracaoAgenteCameraDto ConfigVazia() => new(DateTime.UtcNow, 60, new(), new());

    // ---------- Configuração ----------

    [Fact]
    public async Task Configuracao_SemSegredoOuErrado_Responde401Identico_SemChamarOMediator()
    {
        var semSegredo = Montar(segredo: null);
        var errado = Montar(segredo: "errado");
        var desconhecido = Montar();

        var r1 = await semSegredo.Controller.ObterConfiguracao(semSegredo.Autenticador.Agente.Id, default);
        var r2 = await errado.Controller.ObterConfiguracao(errado.Autenticador.Agente.Id, default);
        var r3 = await desconhecido.Controller.ObterConfiguracao(Guid.NewGuid(), default);

        var corpo = Json(Assert.IsType<UnauthorizedObjectResult>(r1).Value);
        Assert.Equal(corpo, Json(Assert.IsType<UnauthorizedObjectResult>(r2).Value));
        Assert.Equal(corpo, Json(Assert.IsType<UnauthorizedObjectResult>(r3).Value));
        Assert.Empty(semSegredo.Mediator.Comandos);
        Assert.Empty(errado.Mediator.Comandos);
        Assert.Empty(desconhecido.Mediator.Comandos);
    }

    [Fact]
    public async Task Configuracao_ComSegredo_Responde200SemCache_EPassaOAgenteEOSegredoAoMediator()
    {
        var m = Montar();
        m.Http.Request.Headers[AgentesCameraController.CabecalhoVersao] = "1.4.0";
        m.Mediator.Resposta = _ => ConfigVazia();

        var r = await m.Controller.ObterConfiguracao(m.Autenticador.Agente.Id, default);

        Assert.IsType<OkObjectResult>(r);
        Assert.Equal("no-store", m.Http.Response.Headers.CacheControl.ToString());
        var consulta = Assert.IsType<ObterConfiguracaoAgenteCameraQuery>(Assert.Single(m.Mediator.Comandos));
        Assert.Equal(m.Autenticador.Agente.Id, consulta.AgenteId);
        Assert.Equal(Segredo, consulta.Segredo);
        Assert.Equal("1.4.0", consulta.VersaoAgente);
    }

    [Fact]
    public async Task Configuracao_QuandoOHandlerRecusaOAgente_Responde401()
    {
        var m = Montar();
        m.Mediator.Resposta = _ => null;

        Assert.IsType<UnauthorizedObjectResult>(await m.Controller.ObterConfiguracao(m.Autenticador.Agente.Id, default));
    }

    [Fact]
    public async Task Configuracao_ExcedeuACotaDoAgente_Responde429()
    {
        var m = Montar(limitePorMinuto: 1);
        m.Mediator.Resposta = _ => ConfigVazia();

        Assert.IsType<OkObjectResult>(await m.Controller.ObterConfiguracao(m.Autenticador.Agente.Id, default));
        var segunda = await m.Controller.ObterConfiguracao(m.Autenticador.Agente.Id, default);

        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsType<StatusCodeResult>(segunda).StatusCode);
    }

    // ---------- Resultado do teste de conexão ----------

    [Fact]
    public async Task Resultado_SemSegredoOuErrado_Responde401_SemLerOCorpo()
    {
        var semSegredo = Montar(segredo: null);
        var errado = Montar(segredo: "errado");

        var r1 = await semSegredo.Controller.RegistrarResultadoTesteConexao(semSegredo.Autenticador.Agente.Id, Guid.NewGuid(), default);
        var r2 = await errado.Controller.RegistrarResultadoTesteConexao(errado.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.Equal(Json(Assert.IsType<UnauthorizedObjectResult>(r1).Value), Json(Assert.IsType<UnauthorizedObjectResult>(r2).Value));
        Assert.False(semSegredo.Corpo!.FoiLido);
        Assert.False(errado.Corpo!.FoiLido);
        Assert.Empty(semSegredo.Mediator.Comandos);
        Assert.Empty(errado.Mediator.Comandos);
    }

    [Fact]
    public async Task Resultado_Valido_Responde204_ERepassaOsBytesDaMiniatura()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 9, 8 };
        var corpo = Json(new { sucesso = true, motivo = "Imagem recebida", detalhe = "1280x720 H.264", miniaturaBase64 = Convert.ToBase64String(jpeg) });
        var m = Montar(corpoJson: corpo);
        m.Mediator.Resposta = _ => true;
        var testeId = Guid.NewGuid();

        var r = await m.Controller.RegistrarResultadoTesteConexao(m.Autenticador.Agente.Id, testeId, default);

        Assert.IsType<NoContentResult>(r);
        var cmd = Assert.IsType<RegistrarResultadoTesteConexaoCameraCommand>(Assert.Single(m.Mediator.Comandos));
        Assert.Equal(testeId, cmd.TesteId);
        Assert.True(cmd.Sucesso);
        Assert.Equal("1280x720 H.264", cmd.Detalhe);
        Assert.Equal(jpeg, cmd.Miniatura);
        Assert.Equal(Segredo, cmd.Segredo);
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("{\"sucesso\":true,\"miniaturaBase64\":\"@@@\"}")]
    [InlineData("null")]
    public async Task Resultado_CorpoIlegivelOuBase64Invalido_Responde400(string corpo)
    {
        var m = Montar(corpoJson: corpo);

        var r = await m.Controller.RegistrarResultadoTesteConexao(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.IsType<BadRequestObjectResult>(r);
        Assert.Empty(m.Mediator.Comandos);
    }

    [Fact]
    public async Task Resultado_TesteInexistente_Responde404ComCodigoEstavel()
    {
        var m = Montar(corpoJson: Json(new { sucesso = false, motivo = "x" }));
        m.Mediator.Resposta = _ => new KeyNotFoundException("teste_nao_encontrado");

        var r = await m.Controller.RegistrarResultadoTesteConexao(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        var nf = Assert.IsType<NotFoundObjectResult>(r);
        Assert.Equal(Json(new { codigo = "teste_nao_encontrado" }), Json(nf.Value));
    }

    [Fact]
    public async Task Resultado_ValidacaoDaMiniatura_Responde400()
    {
        var m = Montar(corpoJson: Json(new { sucesso = true }));
        m.Mediator.Resposta = _ => new FluentValidation.ValidationException("A miniatura precisa estar em JPEG.");

        var r = await m.Controller.RegistrarResultadoTesteConexao(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.IsType<BadRequestObjectResult>(r);
    }

    // ---------- Estrutura das rotas ----------

    [Theory]
    [InlineData(nameof(AgentesCameraController.ObterConfiguracao), typeof(HttpGetAttribute), "{agenteId:guid}/configuracao")]
    [InlineData(nameof(AgentesCameraController.RegistrarResultadoTesteConexao), typeof(HttpPostAttribute), "{agenteId:guid}/testes-conexao/{testeId:guid}/resultado")]
    public void RotasNovasDoAgente_SaoAnonimas_ComLimitePorIp_SemPolicyDoPiloto_ESemSegredoNaQuery(string nomeAction, Type verbo, string rota)
    {
        var action = typeof(AgentesCameraController).GetMethod(nomeAction)!;

        Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Empty(action.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("agente-camera-ip", action.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Equal(rota, ((Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute)action.GetCustomAttribute(verbo)!).Template);
        Assert.DoesNotContain(action.GetParameters(), p => p.GetCustomAttribute<FromQueryAttribute>() is not null);
        Assert.DoesNotContain(action.GetParameters(), p => p.Name!.Contains("segredo", StringComparison.OrdinalIgnoreCase));
        // O corpo não é ligado por model binding (leria antes de autenticar): só Guid de rota e CancellationToken.
        Assert.All(action.GetParameters(), p => Assert.True(p.ParameterType == typeof(Guid) || p.ParameterType == typeof(CancellationToken)));
    }

    [Fact]
    public void ResultadoDoTeste_TemLimiteDeCorpoMenorQueODaFoto()
    {
        var action = typeof(AgentesCameraController).GetMethod(nameof(AgentesCameraController.RegistrarResultadoTesteConexao))!;

        var limite = action.GetCustomAttribute<RequestSizeLimitAttribute>();
        Assert.NotNull(limite);
        Assert.True(AgentesCameraController.LimiteCorpoResultadoTesteBytes < AgentesCameraController.LimiteCorpoBytes);
    }

    [Fact]
    public void GestaoDeEquipamentosETestes_ExigeOPilotoDeCameras_ESoAdminExcluiPelaPolicy()
    {
        var acoes = new[]
        {
            nameof(CamerasController.ListarEquipamentos), nameof(CamerasController.CriarEquipamento), nameof(CamerasController.AtualizarEquipamento),
            nameof(CamerasController.DefinirSenhaEquipamento), nameof(CamerasController.ExcluirEquipamento),
            nameof(CamerasController.TestarConexaoDaCamera), nameof(CamerasController.TestarConexaoDoEquipamento), nameof(CamerasController.ObterTesteConexao),
        };
        foreach (var nome in acoes)
        {
            var action = typeof(CamerasController).GetMethod(nome)!;
            Assert.Equal(new[] { PoliticasAutorizacao.CamerasPiloto }, action.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy));
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
        }
    }

    // ---------- Escopo: Entra ligado, rota anônima, middleware REAL ----------

    [Fact]
    public async Task EntraLigado_ConfiguracaoDoAgente_FuncionaComEscopoZeradoPeloMiddleware_ESoEnxergaAObraDele()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nameof(EntraLigado_ConfiguracaoDoAgente_FuncionaComEscopoZeradoPeloMiddleware_ESoEnxergaAObraDele)).Options;
        var hasher = new SegredoDispositivoHasherService();
        var cripto = new CredencialCameraCriptografiaService(Enumerable.Repeat((byte)5, 32).ToArray());
        var global = new SstDbContext(opcoes, new CurrentUserService());

        var obraA = new Obra { Codigo = "OA", Nome = "Obra A" };
        var obraB = new Obra { Codigo = "OB", Nome = "Obra B" };
        var segredo = hasher.GerarSegredo();
        var agente = new AgenteCamera { ObraId = obraA.Id, Nome = "PC A", SegredoHash = hasher.GerarHash(segredo) };
        var agenteB = new AgenteCamera { ObraId = obraB.Id, Nome = "PC B", SegredoHash = "x" };
        var equipA = new EquipamentoCamera { ObraId = obraA.Id, AgenteCameraId = agente.Id, Nome = "A31", Host = "192.168.1.50", Usuario = "admin", SenhaCifrada = cripto.Cifrar("senha-A") };
        var equipB = new EquipamentoCamera { ObraId = obraB.Id, AgenteCameraId = agenteB.Id, Nome = "B31", Host = "10.1.1.1", Usuario = "admin", SenhaCifrada = cripto.Cifrar("senha-B") };
        var camA = new Camera { ObraId = obraA.Id, Nome = "Lente A", Fonte = FonteCamera.Rtsp, EquipamentoCameraId = equipA.Id, Canal = 1, Stream = 1 };
        var camB = new Camera { ObraId = obraB.Id, Nome = "Lente B", Fonte = FonteCamera.Rtsp, EquipamentoCameraId = equipB.Id, Canal = 1, Stream = 1 };
        global.AddRange(obraA, obraB, agente, agenteB, equipA, equipB, camA, camB);
        await global.SaveChangesAsync();

        // Requisição anônima com o Entra ligado (TenantId preenchido), exatamente como chega do agente.
        var escopo = new CurrentUserService();
        var db = new SstDbContext(opcoes, escopo);
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AzureAd:TenantId"] = "tenant-qualquer" }).Build();
        await new EscopoPorObraMiddleware(_ => Task.CompletedTask).InvokeAsync(new DefaultHttpContext(), db, escopo, configuracao, acesso: null!);
        Assert.False(escopo.TemAcessoGlobal);
        Assert.Empty(escopo.ObrasPermitidas);
        Assert.Empty(await db.Cameras.ToListAsync()); // prova do problema: o filtro global esconde tudo

        var handler = new ObterConfiguracaoAgenteCameraQueryHandler(db, new AgenteCameraAutenticador(db, hasher), escopo, cripto,
            NullLogger<ObterConfiguracaoAgenteCameraQueryHandler>.Instance);
        var resultado = await handler.Handle(new ObterConfiguracaoAgenteCameraQuery(agente.Id, segredo, "1.0"), default);

        Assert.NotNull(resultado);
        var cam = Assert.Single(resultado.Cameras);
        Assert.Equal(camA.Id, cam.CameraId);
        Assert.Equal("senha-A", cam.Senha);
        Assert.DoesNotContain("senha-B", Json(resultado));
    }
}
