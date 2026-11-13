using System.Reflection;
using System.Text.Json;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Controllers;
using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

// Câmera IP "quase ao vivo": as 3 rotas da tela (policy do piloto, no-store, mapeamento dos códigos) e as 2 rotas anônimas do
// agente (mesma ordem de checagens das demais: segredo antes de ler o corpo; 410/413/404/429; nenhuma resposta de erro leva imagem).
public class CamerasAoVivoRotasTests
{
    private const string Segredo = "segredo-certo";
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xAB, 0xCD, 0xEF, 0x01, 0x23 };

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
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            Comandos.Add(request);
            if (Resposta(request) is Exception ex) throw ex;
            return Task.CompletedTask;
        }
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

    private sealed record Agente(AgentesCameraController Controller, AutenticadorFalso Autenticador, MediatorFalso Mediator, HttpContext Http, CorpoQueNaoPodeSerLido? Sentinela);

    private static Agente MontarAgente(string? segredo = Segredo, byte[]? corpo = null, string? tipo = "image/jpeg", string? json = null)
    {
        var autenticador = new AutenticadorFalso();
        var mediator = new MediatorFalso();
        var controller = new AgentesCameraController(mediator, autenticador, new LimitadorAgenteCamera(), NullLogger<AgentesCameraController>.Instance);
        var http = new DefaultHttpContext();
        CorpoQueNaoPodeSerLido? sentinela = null;
        if (corpo is null && json is null)
        {
            sentinela = new CorpoQueNaoPodeSerLido();
            http.Request.Body = sentinela;
        }
        else
        {
            http.Request.Body = new MemoryStream(corpo ?? System.Text.Encoding.UTF8.GetBytes(json!));
            http.Request.ContentLength = http.Request.Body.Length;
        }
        if (tipo is not null) http.Request.ContentType = tipo;
        if (segredo is not null) http.Request.Headers[AgentesCameraController.CabecalhoSegredo] = segredo;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return new Agente(controller, autenticador, mediator, http, sentinela);
    }

    private static string Json(object? valor) => JsonSerializer.Serialize(valor);

    // Nenhum corpo de erro pode carregar a imagem (nem em bytes, nem em base64, nem como lista de números).
    private static void SemImagem(IActionResult resultado)
    {
        var texto = resultado switch
        {
            ObjectResult o => Json(o.Value),
            FileContentResult => "imagem",
            _ => string.Empty,
        };
        Assert.DoesNotContain(Convert.ToBase64String(Jpeg), texto);
        Assert.DoesNotContain("171,205", texto);
        Assert.DoesNotContain("255,216", texto);
    }

    // ---------- Rotas da tela: policy do piloto ----------

    [Theory]
    [InlineData(nameof(CamerasController.IniciarAoVivo))]
    [InlineData(nameof(CamerasController.ObterQuadroAoVivo))]
    [InlineData(nameof(CamerasController.EncerrarAoVivo))]
    public void RotasDaTela_ExigemAPolicyDoPiloto(string nome)
    {
        var action = typeof(CamerasController).GetMethod(nome)!;
        var politicas = action.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).ToList();
        Assert.True(politicas.SequenceEqual([PoliticasAutorizacao.CamerasPiloto]));
        Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Fact]
    public void RotasDaTela_TemOsCaminhosDoContrato()
    {
        string Rota(string nome) => typeof(CamerasController).GetMethod(nome)!.GetCustomAttributes().OfType<HttpMethodAttribute>().Single().Template!;
        Assert.Equal("{id:guid}/ao-vivo", Rota(nameof(CamerasController.IniciarAoVivo)));
        Assert.Equal("ao-vivo/{sessaoId:guid}/quadro", Rota(nameof(CamerasController.ObterQuadroAoVivo)));
        Assert.Equal("ao-vivo/{sessaoId:guid}/encerrar", Rota(nameof(CamerasController.EncerrarAoVivo)));
    }

    // ---------- Rotas da tela: respostas ----------

    private static CamerasController Tela(MediatorFalso mediator)
    {
        var http = new DefaultHttpContext();
        return new CamerasController(mediator, null!, null!) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    [Fact]
    public async Task Iniciar_Sucesso_Responde200ComASessao_SemCache()
    {
        var mediator = new MediatorFalso { Resposta = _ => new IniciarSessaoAoVivoResultado(new SessaoAoVivoIniciadaDto(Guid.NewGuid(), "Aguardando", 2, 600), null) };
        var c = Tela(mediator);

        var r = await c.IniciarAoVivo(Guid.NewGuid(), default);

        var dto = Assert.IsType<SessaoAoVivoIniciadaDto>(Assert.IsType<OkObjectResult>(r).Value);
        Assert.Equal(2, dto.Fps);
        Assert.Equal("no-store", c.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData("nao_e_camera_ip")]
    [InlineData("camera_desabilitada")]
    [InlineData("agente_revogado")]
    [InlineData("agente_sem_contato")]
    [InlineData("agente_desatualizado")]
    public async Task Iniciar_RecusasDaCamera_Respondem409ComCodigo(string codigo)
    {
        var c = Tela(new MediatorFalso { Resposta = _ => new IniciarSessaoAoVivoResultado(null, codigo) });

        var r = await c.IniciarAoVivo(Guid.NewGuid(), default);

        Assert.Equal(codigo, JsonDocument.Parse(Json(Assert.IsType<ConflictObjectResult>(r).Value)).RootElement.GetProperty("codigo").GetString());
        Assert.Equal("no-store", c.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Iniciar_LimiteDeSessoes_Responde429ComCodigo()
    {
        var c = Tela(new MediatorFalso { Resposta = _ => new IniciarSessaoAoVivoResultado(null, "limite_sessoes") });

        var r = await c.IniciarAoVivo(Guid.NewGuid(), default);

        var o = Assert.IsType<ObjectResult>(r);
        Assert.Equal(StatusCodes.Status429TooManyRequests, o.StatusCode);
        Assert.Contains("limite_sessoes", Json(o.Value));
    }

    [Fact]
    public async Task Quadro_Novo_Responde200JpegComSequenciaECapturadoEm_SemCache()
    {
        var capturado = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
        var c = Tela(new MediatorFalso { Resposta = _ => new QuadroAoVivoDto(SituacaoQuadroAoVivo.Quadro, Jpeg, 42, capturado, null) });

        var r = await c.ObterQuadroAoVivo(Guid.NewGuid(), 41, default);

        var arquivo = Assert.IsType<FileContentResult>(r);
        Assert.Equal("image/jpeg", arquivo.ContentType);
        Assert.Equal(Jpeg, arquivo.FileContents);
        Assert.Equal("42", c.Response.Headers["X-Quadro-Sequencia"].ToString());
        Assert.Equal("2026-11-13T12:00:00.0000000Z", c.Response.Headers["X-Capturado-Em"].ToString());
        Assert.Equal("private, no-store", c.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Quadro_RepassaOAposDaConsulta()
    {
        var mediator = new MediatorFalso { Resposta = _ => new QuadroAoVivoDto(SituacaoQuadroAoVivo.SemNovidade, null, 5, null, null) };
        var id = Guid.NewGuid();

        var r = await Tela(mediator).ObterQuadroAoVivo(id, 5, default);

        Assert.IsType<NoContentResult>(r);
        var q = Assert.IsType<ObterQuadroAoVivoQuery>(Assert.Single(mediator.Comandos));
        Assert.Equal(id, q.SessaoId);
        Assert.Equal(5, q.Apos);
    }

    [Fact]
    public async Task Quadro_Aguardando_Responde202()
    {
        var r = await Tela(new MediatorFalso { Resposta = _ => new QuadroAoVivoDto(SituacaoQuadroAoVivo.Aguardando, null, 0, null, null) })
            .ObterQuadroAoVivo(Guid.NewGuid(), null, default);

        Assert.Equal(StatusCodes.Status202Accepted, Assert.IsType<AcceptedResult>(r).StatusCode);
        Assert.Contains("Aguardando", Json(((AcceptedResult)r).Value));
    }

    [Theory]
    [InlineData("inatividade")]
    [InlineData("tempo_maximo")]
    [InlineData("encerrada")]
    [InlineData("agente_sem_resposta")]
    [InlineData("falha:senha_recusada")]
    public async Task Quadro_Encerrada_Responde410ComMotivo_SemImagem(string motivo)
    {
        var r = await Tela(new MediatorFalso { Resposta = _ => new QuadroAoVivoDto(SituacaoQuadroAoVivo.Encerrada, null, 0, null, motivo) })
            .ObterQuadroAoVivo(Guid.NewGuid(), null, default);

        var o = Assert.IsType<ObjectResult>(r);
        Assert.Equal(StatusCodes.Status410Gone, o.StatusCode);
        Assert.Equal(motivo, JsonDocument.Parse(Json(o.Value)).RootElement.GetProperty("motivo").GetString());
        SemImagem(r);
    }

    [Fact]
    public async Task Encerrar_Responde204_SemCache()
    {
        var mediator = new MediatorFalso();
        var c = Tela(mediator);
        var id = Guid.NewGuid();

        Assert.IsType<NoContentResult>(await c.EncerrarAoVivo(id, default));

        Assert.Equal(id, Assert.IsType<EncerrarSessaoAoVivoCameraCommand>(Assert.Single(mediator.Comandos)).SessaoId);
        Assert.Equal("no-store", c.Response.Headers.CacheControl.ToString());
    }

    // ---------- Rotas do agente ----------

    [Theory]
    [InlineData(nameof(AgentesCameraController.EnviarQuadroAoVivo))]
    [InlineData(nameof(AgentesCameraController.RegistrarFalhaAoVivo))]
    public void RotasDoAgente_SaoAnonimas_ComOLimiteProprioPorIp(string nome)
    {
        var action = typeof(AgentesCameraController).GetMethod(nome)!;
        Assert.NotEmpty(action.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Equal("agente-camera-aovivo", action.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.NotNull(action.GetCustomAttribute<RequestSizeLimitAttribute>());
    }

    [Fact]
    public void RotaDoQuadro_TemOLimiteDuroDe300KB()
        => Assert.Equal(307_200, AgentesCameraController.LimiteCorpoQuadroAoVivoBytes);

    [Fact]
    public async Task EnviarQuadro_SemSegredoOuErrado_Responde401Identico_SemLerOCorpo()
    {
        var semSegredo = MontarAgente(segredo: null);
        var errado = MontarAgente(segredo: "errado");

        var r1 = await semSegredo.Controller.EnviarQuadroAoVivo(semSegredo.Autenticador.Agente.Id, Guid.NewGuid(), default);
        var r2 = await errado.Controller.EnviarQuadroAoVivo(errado.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.Equal(Json(Assert.IsType<UnauthorizedObjectResult>(r1).Value), Json(Assert.IsType<UnauthorizedObjectResult>(r2).Value));
        Assert.False(semSegredo.Sentinela!.FoiLido);
        Assert.False(errado.Sentinela!.FoiLido);
        Assert.Empty(semSegredo.Mediator.Comandos);
        Assert.Empty(errado.Mediator.Comandos);
    }

    [Fact]
    public async Task EnviarQuadro_Valido_Responde204_ERepassaOsBytesSemMexer()
    {
        var m = MontarAgente(corpo: Jpeg);
        m.Http.Request.Headers["X-Capturado-Em"] = "2026-11-13T12:00:00Z";
        m.Mediator.Resposta = _ => ResultadoQuadroAoVivo.Continuar;
        var sessaoId = Guid.NewGuid();

        var r = await m.Controller.EnviarQuadroAoVivo(m.Autenticador.Agente.Id, sessaoId, default);

        Assert.IsType<NoContentResult>(r);
        var cmd = Assert.IsType<RegistrarQuadroAoVivoAgenteCommand>(Assert.Single(m.Mediator.Comandos));
        Assert.Equal(sessaoId, cmd.SessaoId);
        Assert.Equal(Jpeg, cmd.Jpeg);
        Assert.Equal(Segredo, cmd.Segredo);
        Assert.Equal(new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc), cmd.CapturadoEmUtc);
    }

    [Fact]
    public async Task EnviarQuadro_NaoUsaOLimitePorAgenteDaAnalise()
    {
        // O limitador da análise (600/min) não pode esgotar com a transmissão: a rota ao vivo nem o consulta.
        var autenticador = new AutenticadorFalso();
        var mediator = new MediatorFalso { Resposta = _ => ResultadoQuadroAoVivo.Continuar };
        var controller = new AgentesCameraController(mediator, autenticador, new LimitadorAgenteCamera(limitePorMinuto: 1), NullLogger<AgentesCameraController>.Instance);
        for (var i = 0; i < 3; i++)
        {
            var http = new DefaultHttpContext();
            http.Request.Body = new MemoryStream(Jpeg);
            http.Request.ContentType = "image/jpeg";
            http.Request.Headers[AgentesCameraController.CabecalhoSegredo] = Segredo;
            controller.ControllerContext = new ControllerContext { HttpContext = http };
            Assert.IsType<NoContentResult>(await controller.EnviarQuadroAoVivo(autenticador.Agente.Id, Guid.NewGuid(), default));
        }
    }

    [Fact]
    public async Task EnviarQuadro_SessaoEncerrada_Responde410ComCodigo_SemImagem()
    {
        var m = MontarAgente(corpo: Jpeg);
        m.Mediator.Resposta = _ => ResultadoQuadroAoVivo.SessaoEncerrada;

        var r = await m.Controller.EnviarQuadroAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        var o = Assert.IsType<ObjectResult>(r);
        Assert.Equal(StatusCodes.Status410Gone, o.StatusCode);
        Assert.Contains("sessao_encerrada", Json(o.Value));
        SemImagem(r);
    }

    [Fact]
    public async Task EnviarQuadro_AcimaDe300KB_Responde413_SemChamarOMediator()
    {
        var m = MontarAgente(corpo: new byte[307_201]);

        var r = await m.Controller.EnviarQuadroAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsType<StatusCodeResult>(r).StatusCode);
        Assert.Empty(m.Mediator.Comandos);
    }

    [Fact]
    public async Task EnviarQuadro_SemContentLengthMasAcimaDe300KB_Responde413()
    {
        var m = MontarAgente(corpo: new byte[400_000]);
        m.Http.Request.ContentLength = null; // chunked: o tamanho só aparece lendo

        var r = await m.Controller.EnviarQuadroAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsType<StatusCodeResult>(r).StatusCode);
        Assert.Empty(m.Mediator.Comandos);
    }

    [Fact]
    public async Task EnviarQuadro_Exatamente300KB_Passa()
    {
        var corpo = new byte[307_200];
        corpo[0] = 0xFF;
        corpo[1] = 0xD8;
        var m = MontarAgente(corpo: corpo);
        m.Mediator.Resposta = _ => ResultadoQuadroAoVivo.Continuar;

        Assert.IsType<NoContentResult>(await m.Controller.EnviarQuadroAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task EnviarQuadro_SessaoDeOutroAgenteOuInexistente_Responde404ComCodigo_SemImagem()
    {
        var m = MontarAgente(corpo: Jpeg);
        m.Mediator.Resposta = _ => new KeyNotFoundException("sessao_nao_encontrada");

        var r = await m.Controller.EnviarQuadroAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.Contains("sessao_nao_encontrada", Json(Assert.IsType<NotFoundObjectResult>(r).Value));
        SemImagem(r);
    }

    [Fact]
    public async Task EnviarQuadro_TetoDaSessao_Responde429()
    {
        var m = MontarAgente(corpo: Jpeg);
        m.Mediator.Resposta = _ => ResultadoQuadroAoVivo.LimiteExcedido;

        var r = await m.Controller.EnviarQuadroAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsType<StatusCodeResult>(r).StatusCode);
    }

    [Fact]
    public async Task EnviarQuadro_QuandoOHandlerRecusaOAgente_Responde401()
    {
        var m = MontarAgente(corpo: Jpeg);
        m.Mediator.Resposta = _ => ResultadoQuadroAoVivo.NaoAutenticado;

        Assert.IsType<UnauthorizedObjectResult>(await m.Controller.EnviarQuadroAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default));
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("application/json")]
    [InlineData(null)]
    public async Task EnviarQuadro_ContentTypeQueNaoEJpeg_Responde400_SemImagem(string? tipo)
    {
        var m = MontarAgente(corpo: Jpeg, tipo: tipo);

        var r = await m.Controller.EnviarQuadroAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.IsType<BadRequestObjectResult>(r);
        Assert.Empty(m.Mediator.Comandos);
        SemImagem(r);
    }

    [Fact]
    public async Task EnviarQuadro_CorpoVazio_Responde400()
    {
        var m = MontarAgente(corpo: Array.Empty<byte>());
        Assert.IsType<BadRequestObjectResult>(await m.Controller.EnviarQuadroAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task EnviarQuadro_NaoEJpegPeloValidador_Responde400_SemImagem()
    {
        var m = MontarAgente(corpo: Jpeg);
        m.Mediator.Resposta = _ => new FluentValidation.ValidationException("O quadro precisa estar em JPEG.");

        var r = await m.Controller.EnviarQuadroAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.IsType<BadRequestObjectResult>(r);
        SemImagem(r);
    }

    [Fact]
    public void Validador_RecusaCorpoQueNaoEJpeg()
    {
        var validador = new RegistrarQuadroAoVivoAgenteCommandValidator();
        Assert.True(validador.Validate(new RegistrarQuadroAoVivoAgenteCommand(Guid.NewGuid(), "s", null, Guid.NewGuid(), Jpeg, null)).IsValid);
        Assert.False(validador.Validate(new RegistrarQuadroAoVivoAgenteCommand(Guid.NewGuid(), "s", null, Guid.NewGuid(), new byte[] { 1, 2, 3, 4, 5 }, null)).IsValid);
        Assert.False(validador.Validate(new RegistrarQuadroAoVivoAgenteCommand(Guid.NewGuid(), "s", null, Guid.NewGuid(), Array.Empty<byte>(), null)).IsValid);
    }

    [Fact]
    public void ComandoDoQuadro_ToStringNaoLevaImagem()
    {
        var texto = new RegistrarQuadroAoVivoAgenteCommand(Guid.NewGuid(), "segredo-secreto", null, Guid.NewGuid(), Jpeg, null).ToString();
        Assert.DoesNotContain("segredo-secreto", texto);
        Assert.DoesNotContain("171", texto);
    }

    [Fact]
    public async Task Falha_SemSegredo_Responde401_SemLerOCorpo()
    {
        var m = MontarAgente(segredo: null, tipo: "application/json");

        var r = await m.Controller.RegistrarFalhaAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.IsType<UnauthorizedObjectResult>(r);
        Assert.False(m.Sentinela!.FoiLido);
        Assert.Empty(m.Mediator.Comandos);
    }

    [Fact]
    public async Task Falha_Valida_Responde204_ERepassaOCodigo()
    {
        var m = MontarAgente(json: Json(new { codigo = "senha_recusada", motivo = "algo" }), tipo: "application/json");
        m.Mediator.Resposta = _ => true;
        var sessaoId = Guid.NewGuid();

        var r = await m.Controller.RegistrarFalhaAoVivo(m.Autenticador.Agente.Id, sessaoId, default);

        Assert.IsType<NoContentResult>(r);
        var cmd = Assert.IsType<RegistrarFalhaAoVivoAgenteCommand>(Assert.Single(m.Mediator.Comandos));
        Assert.Equal(sessaoId, cmd.SessaoId);
        Assert.Equal("senha_recusada", cmd.Codigo);
    }

    [Fact]
    public async Task Falha_SessaoInexistente_Responde404ComCodigo()
    {
        var m = MontarAgente(json: Json(new { codigo = "sem_resposta" }), tipo: "application/json");
        m.Mediator.Resposta = _ => new KeyNotFoundException("sessao_nao_encontrada");

        var r = await m.Controller.RegistrarFalhaAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default);

        Assert.Contains("sessao_nao_encontrada", Json(Assert.IsType<NotFoundObjectResult>(r).Value));
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("null")]
    public async Task Falha_CorpoIlegivel_Responde400(string corpo)
    {
        var m = MontarAgente(json: corpo, tipo: "application/json");
        Assert.IsType<BadRequestObjectResult>(await m.Controller.RegistrarFalhaAoVivo(m.Autenticador.Agente.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public void ConfiguracaoDoAgente_AoVivoSerializaNoFormatoDoContrato()
    {
        var sessao = Guid.NewGuid();
        var camera = Guid.NewGuid();
        var dto = new ConfiguracaoAgenteCameraDto(DateTime.UtcNow, 15, new(), new(), new() { new AoVivoDoAgenteDto(sessao, camera, 2, 640, 540) });

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var item = JsonDocument.Parse(json).RootElement.GetProperty("aoVivo")[0];

        Assert.Equal(sessao, item.GetProperty("sessaoId").GetGuid());
        Assert.Equal(camera, item.GetProperty("cameraId").GetGuid());
        Assert.Equal(2, item.GetProperty("fps").GetInt32());
        Assert.Equal(640, item.GetProperty("larguraMaxima").GetInt32());
        Assert.Equal(540, item.GetProperty("segundosRestantes").GetInt32());
    }
}
