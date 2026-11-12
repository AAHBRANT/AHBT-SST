using System.Reflection;
using System.Text.Json;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Controllers;
using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

// Contrato da rota anônima do agente de câmeras: autenticar ANTES de ler o corpo, limite por agente só para quem se
// autentica, 404 com código estável e nenhum dado de ocorrência/trabalhador em qualquer resposta.
public class AgentesCameraControllerTests
{
    private const string Segredo = "segredo-certo";
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0x10, 0x20 };

    private sealed class AutenticadorFalso : IAgenteCameraAutenticador
    {
        private readonly AgenteCamera _agente = new() { ObraId = Guid.NewGuid(), Nome = "PC" };
        public AgenteCamera Agente => _agente;
        public int Chamadas { get; private set; }

        public Task<AgenteCamera?> ValidarAsync(Guid agenteId, string? segredo, CancellationToken ct)
        {
            Chamadas++;
            return Task.FromResult<AgenteCamera?>(agenteId == _agente.Id && segredo == Segredo ? _agente : null);
        }
    }

    private sealed class MediatorFalso : IMediator
    {
        public Func<object, object?> Resposta { get; set; } = _ => new ResultadoQuadroDto(SituacaoQuadro.Limpo, null, false, 1, 0, 0);
        public List<object> Comandos { get; } = new();

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Comandos.Add(request);
            return Task.FromResult((TResponse)Resposta(request)!);
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }

    // Corpo que acusa qualquer leitura: prova que a requisição não autenticada não foi bufferizada.
    private sealed class CorpoQueNaoPodeSerLido : MemoryStream
    {
        public bool FoiLido { get; private set; }
        public override int Read(byte[] buffer, int offset, int count) { FoiLido = true; throw new InvalidOperationException("corpo lido"); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { FoiLido = true; throw new InvalidOperationException("corpo lido"); }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { FoiLido = true; throw new InvalidOperationException("corpo lido"); }
    }

    private sealed record Montagem(AgentesCameraController Controller, AutenticadorFalso Autenticador, MediatorFalso Mediator, CorpoQueNaoPodeSerLido Corpo);

    private static Montagem Montar(int limitePorMinuto = LimitadorAgenteCamera.LimitePadraoPorMinuto, string? segredo = Segredo, bool comFormulario = true)
    {
        var autenticador = new AutenticadorFalso();
        var mediator = new MediatorFalso();
        var corpo = new CorpoQueNaoPodeSerLido();
        var controller = new AgentesCameraController(mediator, autenticador, new LimitadorAgenteCamera(limitePorMinuto), NullLogger<AgentesCameraController>.Instance);
        var http = new DefaultHttpContext();
        http.Request.Body = corpo;
        http.Request.ContentType = "multipart/form-data; boundary=x";
        if (segredo is not null) http.Request.Headers[AgentesCameraController.CabecalhoSegredo] = segredo;
        if (comFormulario)
        {
            var arquivos = new FormFileCollection { new FormFile(new MemoryStream(Jpeg), 0, Jpeg.Length, "foto", "q.jpg") };
            http.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), arquivos);
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return new Montagem(controller, autenticador, mediator, corpo);
    }

    private static string Json(object? valor) => JsonSerializer.Serialize(valor);

    [Fact]
    public async Task SemSegredo_Responde401_SemLerOCorpoNemChamarOMediator()
    {
        var m = Montar(segredo: null, comFormulario: false);

        var r = await m.Controller.EnviarQuadro(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.IsType<UnauthorizedObjectResult>(r);
        Assert.False(m.Corpo.FoiLido);
        Assert.Empty(m.Mediator.Comandos);
    }

    [Fact]
    public async Task SegredoErradoOuAgenteDesconhecido_Responde401IdenticoSemLerOCorpo()
    {
        var errado = Montar(segredo: "errado", comFormulario: false);
        var desconhecido = Montar(comFormulario: false);

        var r1 = await errado.Controller.EnviarQuadro(errado.Autenticador.Agente.Id, Guid.NewGuid(), default);
        var r2 = await desconhecido.Controller.EnviarQuadro(Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.Equal(Json(Assert.IsType<UnauthorizedObjectResult>(r1).Value), Json(Assert.IsType<UnauthorizedObjectResult>(r2).Value));
        Assert.False(errado.Corpo.FoiLido);
        Assert.False(desconhecido.Corpo.FoiLido);
    }

    [Fact]
    public async Task ChamadasSemSegredoNaoConsomemACotaDoAgenteLegitimo()
    {
        var legitimo = Montar(limitePorMinuto: 2);
        var agenteId = legitimo.Autenticador.Agente.Id;

        // O atacante conhece o GUID do agente, mas não o segredo: as chamadas dele não contam na cota.
        for (var i = 0; i < 50; i++)
        {
            Assert.IsType<UnauthorizedObjectResult>(await legitimo.Controller.EnviarQuadroComSegredo("chute", agenteId));
        }

        Assert.IsType<OkObjectResult>(await legitimo.Controller.EnviarQuadro(agenteId, Guid.NewGuid(), default));
        Assert.IsType<OkObjectResult>(await legitimo.Controller.EnviarQuadro(agenteId, Guid.NewGuid(), default));
        var terceira = await legitimo.Controller.EnviarQuadro(agenteId, Guid.NewGuid(), default);
        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsType<StatusCodeResult>(terceira).StatusCode);
    }

    [Fact]
    public void Limitador_CotaEPorAgente()
    {
        var limitador = new LimitadorAgenteCamera(1);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Assert.True(limitador.TentarObter(a));
        Assert.False(limitador.TentarObter(a));
        Assert.True(limitador.TentarObter(b));
    }

    [Fact]
    public async Task AgenteValido_EnviaAFotoAoMediator_ERetornaSoOResultadoDoQuadro()
    {
        var m = Montar();
        m.Mediator.Resposta = _ => new ResultadoQuadroDto(SituacaoQuadro.Ocorrencia, Guid.NewGuid(), true, 1, 1, 0, Guid.NewGuid(), "Fulano");

        var r = await m.Controller.EnviarQuadro(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.IsType<ResultadoQuadroDto>(Assert.IsType<OkObjectResult>(r).Value);
        var comando = Assert.IsType<AnalisarQuadroAgenteCameraCommand>(Assert.Single(m.Mediator.Comandos));
        Assert.Equal(Jpeg, comando.Imagem);
        Assert.Equal(Segredo, comando.Segredo);
    }

    [Fact]
    public async Task CameraNaoEncontrada_Responde404ComCodigoEstavel()
    {
        var m = Montar();
        m.Mediator.Resposta = _ => throw new KeyNotFoundException("Câmera X não encontrada.");

        var r = await m.Controller.EnviarQuadro(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        var nf = Assert.IsType<NotFoundObjectResult>(r);
        Assert.Equal("{\"codigo\":\"camera_nao_encontrada\"}", Json(nf.Value));
        Assert.Equal("camera_nao_encontrada", AgentesCameraController.CodigoCameraNaoEncontrada);
    }

    // O 409 do middleware devolveria todas as colunas da ocorrência (Foto, TrabalhadorId...). Nesta rota vira 503 sem corpo.
    [Fact]
    public async Task ConflitoDeConcorrencia_Responde503SemCorpo_SemDadosDeOcorrencia()
    {
        var m = Montar();
        m.Mediator.Resposta = _ => throw new DbUpdateConcurrencyException("conflito com dados sensíveis");

        var r = await m.Controller.EnviarQuadro(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        var codigo = Assert.IsType<StatusCodeResult>(r);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo.StatusCode);
    }

    [Fact]
    public async Task SemFoto_Responde400()
    {
        var m = Montar(comFormulario: false);
        m.Controller.HttpContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());

        var r = await m.Controller.EnviarQuadro(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.IsType<BadRequestObjectResult>(r);
        Assert.Empty(m.Mediator.Comandos);
    }

    [Fact]
    public void Rota_TemLimitePorIp_CorpoPequeno_ENaoUsaModelBindingDeFormulario()
    {
        var action = typeof(AgentesCameraController).GetMethod(nameof(AgentesCameraController.EnviarQuadro))!;

        Assert.Equal("agente-camera-ip", action.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        // Foto de 4 MB + envelope do multipart, não os 6 MB de antes.
        var limite = action.GetCustomAttribute<RequestSizeLimitAttribute>()!;
        Assert.InRange(AgentesCameraController.LimiteCorpoBytes, 4_000_000 + 1, 4_500_000);
        // Sem [FromForm]/IFormFile nos parâmetros: o model binding leria o multipart antes de autenticar.
        Assert.All(action.GetParameters(), p =>
        {
            Assert.Null(p.GetCustomAttribute<FromFormAttribute>());
            Assert.NotEqual(typeof(IFormFile), p.ParameterType);
            Assert.NotEqual(typeof(QuadroCameraForm), p.ParameterType);
        });
        Assert.NotNull(limite);
    }
}

internal static class AgentesCameraControllerTestesExtensoes
{
    // Chamada com outro segredo no mesmo controller (e mesmo limitador), sem corpo: simula quem chuta o segredo.
    public static async Task<IActionResult> EnviarQuadroComSegredo(this AgentesCameraController controller, string segredo, Guid agenteId)
    {
        var original = controller.HttpContext.Request.Headers[AgentesCameraController.CabecalhoSegredo];
        controller.HttpContext.Request.Headers[AgentesCameraController.CabecalhoSegredo] = segredo;
        try { return await controller.EnviarQuadro(agenteId, Guid.NewGuid(), default); }
        finally { controller.HttpContext.Request.Headers[AgentesCameraController.CabecalhoSegredo] = original; }
    }
}
