using System.Globalization;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Api.Controllers;

// Rotas chamadas pelo agente de câmeras instalado na obra (serviço Windows), sem usuário logado. Ficam fora de
// CamerasController de propósito: lá toda action exige a policy CamerasPiloto (e há teste que garante isso).
// A autenticação é o segredo no cabeçalho X-Agente-Segredo; nunca em query string.
//
// Contrato de EnviarQuadro (multipart/form-data: "foto" JPEG até 4 MB; "capturadaEm" opcional, ISO 8601):
//   200  ResultadoQuadroDto (o mesmo da tela Monitorar).
//   400  validação (foto ausente/inválida/grande demais, capturadaEm ilegível).
//   401  segredo ausente, errado, agente inexistente, revogado OU de obra inativa: sempre o mesmo corpo, sem distinguir.
//   404  { "codigo": "camera_nao_encontrada" }: câmera inexistente ou de outra obra. O código é estável: o agente o usa
//        para separar este 404 de um 404 de roteamento/infra (proxy, rota errada, API antiga), que não traz o código.
//   429  limite por IP (policy agente-camera-ip) ou por agente autenticado (LimitadorAgenteCamera).
//   503  conflito de gravação transitório (o técnico mexeu na ocorrência no mesmo instante); sem corpo. Reenviar o quadro.
//
// Contrato de ObterConfiguracao (GET {agenteId}/configuracao, sem corpo; cabeçalhos X-Agente-Segredo e X-Agente-Versao):
//   200  ConfiguracaoAgenteCameraDto: { geradaEmUtc, intervaloConsultaSegundos, cameras[], testesConexao[] }.
//        cameras = câmeras IP (Fonte Rtsp) ATIVAS e habilitadas deste agente, com host, portaRtsp, portaOnvif, enderecoMac, usuario,
//        senha (decifrada: é o único lugar do sistema que a devolve), canal, stream e intervaloEnvioSegundos. Câmera de equipamento
//        sem senha utilizável não entra. testesConexao = pedidos de "Testar conexão" em aberto (mesmos campos de conexão, mais testeId).
//        Cache-Control: no-store. Esta consulta é o sinal de vida do agente: repetir a cada intervaloConsultaSegundos (15 s).
//   401  segredo ausente/errado/agente revogado ou de obra inativa: o mesmo corpo de sempre.
//   429  limite por IP ou por agente.
//
// Contrato de RegistrarResultadoTesteConexao (POST {agenteId}/testes-conexao/{testeId}/resultado, application/json até 256 KB):
//   corpo { "sucesso": bool, "codigo": string?, "motivo": string?, "detalhe": string?, "miniaturaBase64": string? } (miniatura: JPEG até 150 KB, só vale se sucesso).
//   codigo (só na falha): senha_recusada, acesso_negado, camera_nao_encontrada, canal_ou_stream_invalido, conexao_recusada,
//   conexao_encerrada, sem_resposta, ffmpeg_ausente, sem_imagem, imagem_ilegivel, erro_desconhecido; fora da lista vira erro_desconhecido.
//   Miniatura de resultado que chega depois de o pedido expirar (2 min) é descartada, mas o resultado responde 204 do mesmo jeito.
//   204  registrado (reenvio do mesmo resultado também responde 204 e mantém o primeiro).
//   400  corpo ilegível ou miniatura inválida.   401  igual acima.
//   404  { "codigo": "teste_nao_encontrado" }: teste inexistente ou de outro agente.   429  limites.
//
// Contrato de EnviarQuadroAoVivo (POST {agenteId}/ao-vivo/{sessaoId}/quadros, corpo image/jpeg CRU até 300 KB, sem multipart;
// cabeçalho X-Capturado-Em opcional, ISO 8601; ilegível ou no futuro além de 5 min = vale o horário do servidor):
//   204  gravado: continuar.
//   400  corpo vazio, fora de JPEG ou Content-Type diferente de image/jpeg.
//   401  igual acima.
//   404  { "codigo": "sessao_nao_encontrada" }: sessão inexistente, de outra obra OU de outro agente (indistinguíveis).
//   410  { "codigo": "sessao_encerrada" }: o agente PARA já esta transmissão (tela fechou, tempo, inatividade, câmera desligada...).
//   413  corpo acima do limite (300 KB, Cameras:AoVivo:TamanhoMaximoQuadroBytes).
//   429  limite por IP (policy agente-camera-aovivo, própria e separada da análise) ou teto de quadros da sessão (fps x 60 x 1,2/min).
//   Não há o limite por agente da análise (LimitadorAgenteCamera): a transmissão não pode consumir a cota dos quadros de EPI.
//   Nenhuma resposta, de sucesso ou erro, carrega bytes de imagem; o quadro nunca vai para log.
//
// Contrato de RegistrarFalhaAoVivo (POST {agenteId}/ao-vivo/{sessaoId}/falha, application/json até 4 KB):
//   corpo { "codigo": string?, "motivo": string? }: codigo de CodigosTesteConexaoCamera (outro vira erro_desconhecido); motivo é descartado.
//   204  sessão encerrada com o código da falha (ou já estava encerrada).   400 corpo ilegível.   401 igual acima.
//   404  { "codigo": "sessao_nao_encontrada" }.   429 limite por IP.
//
// Nenhuma resposta desta rota carrega dado de ocorrência ou de trabalhador além do ResultadoQuadroDto: não passa pelo
// 409 de conflito do TratamentoDeExcecaoMiddleware, que devolve todas as colunas da linha (inclusive a foto).
[ApiController]
[Route("api/agentes-camera")]
public class AgentesCameraController : ControllerBase
{
    public const string CabecalhoSegredo = "X-Agente-Segredo";
    public const string CabecalhoVersao = "X-Agente-Versao";
    public const string CodigoCameraNaoEncontrada = "camera_nao_encontrada";
    public const string CodigoTesteNaoEncontrado = RegistrarResultadoTesteConexaoCameraCommandHandler.CodigoTesteNaoEncontrado;
    public const string CodigoSessaoNaoEncontrada = RegistrarQuadroAoVivoAgenteCommandHandler.CodigoSessaoNaoEncontrada;
    public const string CodigoSessaoEncerrada = "sessao_encerrada";

    // Quadro ao vivo: JPEG cru de até 300 KB (teto duro do RequestSizeLimit; o valor configurável só pode ficar abaixo dele).
    public const int LimiteCorpoQuadroAoVivoBytes = 307_200;
    public const int LimiteCorpoFalhaAoVivoBytes = 4_096;

    // JSON do resultado do teste: miniatura de até 150 KB em base64 (~200 KB) mais o envelope.
    public const int LimiteCorpoResultadoTesteBytes = 262_144;

    // Foto de até 4 MB (AnalisarQuadroCameraCommandValidator.TamanhoMaximoBytes) mais o envelope do multipart.
    public const int LimiteCorpoBytes = 4_200_000;

    // Quadro com horário além disto no futuro é relógio errado do PC da obra: usa o horário do servidor.
    private static readonly TimeSpan ToleranciaRelogio = TimeSpan.FromMinutes(5);

    private readonly IMediator _mediator;
    private readonly IAgenteCameraAutenticador _autenticador;
    private readonly LimitadorAgenteCamera _limitador;
    private readonly ILogger<AgentesCameraController> _logger;
    private readonly AoVivoCameraOpcoes _opcoesAoVivo;

    public AgentesCameraController(
        IMediator mediator, IAgenteCameraAutenticador autenticador, LimitadorAgenteCamera limitador, ILogger<AgentesCameraController> logger,
        Microsoft.Extensions.Options.IOptions<AoVivoCameraOpcoes>? opcoesAoVivo = null)
    {
        _opcoesAoVivo = opcoesAoVivo?.Value ?? new AoVivoCameraOpcoes();
        _mediator = mediator;
        _autenticador = autenticador;
        _limitador = limitador;
        _logger = logger;
    }

    // Ordem: limite por IP (antes da action) -> autenticação pelo cabeçalho -> limite do agente -> SÓ ENTÃO lê o corpo.
    // O corpo é lido à mão (Request.ReadFormAsync) e não por [FromForm]: o model binding leria e copiaria o multipart
    // (até 4 MB) antes de a action rodar, e qualquer um que não tem o segredo faria o servidor bufferizar fotos.
    [AllowAnonymous]
    [EnableRateLimiting("agente-camera-ip")]
    [HttpPost("{agenteId:guid}/cameras/{cameraId:guid}/quadros")]
    [RequestSizeLimit(LimiteCorpoBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteCorpoBytes)]
    public async Task<IActionResult> EnviarQuadro(Guid agenteId, Guid cameraId, CancellationToken ct)
    {
        var segredo = Request.Headers[CabecalhoSegredo].ToString();
        var agente = await _autenticador.ValidarAsync(agenteId, segredo, ct);
        if (agente is null) return NaoAutorizado();

        if (!_limitador.TentarObter(agente.Id)) return StatusCode(StatusCodes.Status429TooManyRequests);

        if (!Request.HasFormContentType) return BadRequest(new { erro = "Envie o quadro como multipart/form-data." });
        IFormCollection form;
        try
        {
            form = await Request.ReadFormAsync(ct);
        }
        catch (BadHttpRequestException ex) // corpo acima do limite (413) ou ilegível
        {
            return StatusCode(ex.StatusCode);
        }
        catch (InvalidDataException)
        {
            return BadRequest(new { erro = "Corpo multipart inválido." });
        }

        var foto = form.Files.GetFile("foto");
        if (foto is null || foto.Length == 0) return BadRequest(new { erro = "Envie a foto do quadro." });

        var agora = DateTimeOffset.UtcNow;
        var capturadaEm = agora;
        var informada = form["capturadaEm"].ToString();
        if (!string.IsNullOrWhiteSpace(informada))
        {
            if (!DateTimeOffset.TryParse(informada, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var c))
                return BadRequest(new { erro = "Horário de captura inválido." });
            if (c <= agora + ToleranciaRelogio) capturadaEm = c;
        }

        using var memoria = new MemoryStream();
        await foto.CopyToAsync(memoria, ct);
        var versao = Request.Headers[CabecalhoVersao].ToString();

        try
        {
            var resultado = await _mediator.Send(new AnalisarQuadroAgenteCameraCommand(
                agente.Id, segredo, versao, cameraId, memoria.ToArray(), capturadaEm.UtcDateTime), ct);

            return resultado is null ? NaoAutorizado() : Ok(resultado);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { codigo = CodigoCameraNaoEncontrada });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Sem corpo: o middleware global devolveria dadosAtuais (todas as colunas da ocorrência, com Foto e TrabalhadorId).
            _logger.LogWarning(ex, "Conflito de gravação ao receber quadro do agente de câmeras {AgenteId}.", agente.Id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    // Mesma ordem da rota de quadros: limite por IP -> autenticação pelo cabeçalho -> limite do agente -> só então consulta.
    [AllowAnonymous]
    [EnableRateLimiting("agente-camera-ip")]
    [HttpGet("{agenteId:guid}/configuracao")]
    public async Task<IActionResult> ObterConfiguracao(Guid agenteId, CancellationToken ct)
    {
        var segredo = Request.Headers[CabecalhoSegredo].ToString();
        var agente = await _autenticador.ValidarAsync(agenteId, segredo, ct);
        if (agente is null) return NaoAutorizado();

        if (!_limitador.TentarObter(agente.Id)) return StatusCode(StatusCodes.Status429TooManyRequests);

        var configuracao = await _mediator.Send(
            new ObterConfiguracaoAgenteCameraQuery(agente.Id, segredo, Request.Headers[CabecalhoVersao].ToString()), ct);
        if (configuracao is null) return NaoAutorizado();

        // Leva a senha das câmeras: nada de cache em navegador, proxy ou CDN.
        Response.Headers.CacheControl = "no-store";
        return Ok(configuracao);
    }

    // O corpo é lido à mão, depois da autenticação: o model binding leria o JSON antes da action e qualquer um sem o segredo
    // faria o servidor bufferizar até o limite do corpo.
    [AllowAnonymous]
    [EnableRateLimiting("agente-camera-ip")]
    [HttpPost("{agenteId:guid}/testes-conexao/{testeId:guid}/resultado")]
    [RequestSizeLimit(LimiteCorpoResultadoTesteBytes)]
    public async Task<IActionResult> RegistrarResultadoTesteConexao(Guid agenteId, Guid testeId, CancellationToken ct)
    {
        var segredo = Request.Headers[CabecalhoSegredo].ToString();
        var agente = await _autenticador.ValidarAsync(agenteId, segredo, ct);
        if (agente is null) return NaoAutorizado();

        if (!_limitador.TentarObter(agente.Id)) return StatusCode(StatusCodes.Status429TooManyRequests);

        if (!Request.HasJsonContentType()) return BadRequest(new { erro = "Envie o resultado como application/json." });
        ResultadoTesteConexaoRequest? corpo;
        try
        {
            corpo = await Request.ReadFromJsonAsync<ResultadoTesteConexaoRequest>(ct);
        }
        catch (BadHttpRequestException ex) // corpo acima do limite (413)
        {
            return StatusCode(ex.StatusCode);
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(new { erro = "Corpo JSON inválido." });
        }
        if (corpo is null) return BadRequest(new { erro = "Informe o resultado do teste." });

        byte[]? miniatura = null;
        if (!string.IsNullOrWhiteSpace(corpo.MiniaturaBase64))
        {
            try
            {
                miniatura = Convert.FromBase64String(corpo.MiniaturaBase64);
            }
            catch (FormatException)
            {
                return BadRequest(new { erro = "Miniatura em base64 inválida." });
            }
        }

        try
        {
            var registrado = await _mediator.Send(new RegistrarResultadoTesteConexaoCameraCommand(
                agente.Id, segredo, Request.Headers[CabecalhoVersao].ToString(), testeId, corpo.Sucesso, corpo.Motivo, corpo.Detalhe, miniatura, corpo.Codigo), ct);
            return registrado ? NoContent() : NaoAutorizado();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Outra gravação da mesma linha no mesmo instante (reenvio do agente, ou a tela consumindo a miniatura): o resultado
            // já está registrado. Responde como reenvio, sem corpo: o middleware global devolveria a linha (com a miniatura).
            _logger.LogWarning(ex, "Conflito de gravação ao registrar o resultado do teste {TesteId} do agente {AgenteId}; tratado como reenvio.", testeId, agente.Id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { codigo = CodigoTesteNaoEncontrado });
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { erro = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)) });
        }
    }

    // Ordem: limite por IP (antes da action) -> autenticação -> SÓ ENTÃO lê o corpo (quem não tem o segredo não faz o servidor
    // bufferizar nada). A leitura é limitada ao tamanho máximo: acima dele responde 413 sem guardar o resto.
    [AllowAnonymous]
    [EnableRateLimiting("agente-camera-aovivo")]
    [HttpPost("{agenteId:guid}/ao-vivo/{sessaoId:guid}/quadros")]
    [RequestSizeLimit(LimiteCorpoQuadroAoVivoBytes)]
    public async Task<IActionResult> EnviarQuadroAoVivo(Guid agenteId, Guid sessaoId, CancellationToken ct)
    {
        var segredo = Request.Headers[CabecalhoSegredo].ToString();
        var agente = await _autenticador.ValidarAsync(agenteId, segredo, ct);
        if (agente is null) return NaoAutorizado();

        var tipo = Request.ContentType;
        if (tipo is null || !tipo.StartsWith("image/jpeg", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { erro = "Envie o quadro como image/jpeg." });

        var limite = Math.Min(Math.Max(1, _opcoesAoVivo.TamanhoMaximoQuadroBytes), LimiteCorpoQuadroAoVivoBytes);
        if (Request.ContentLength is { } declarado && declarado > limite) return StatusCode(StatusCodes.Status413PayloadTooLarge);

        byte[] jpeg;
        try
        {
            using var memoria = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int lidos;
            while ((lidos = await Request.Body.ReadAsync(buffer, ct)) > 0)
            {
                if (memoria.Length + lidos > limite) return StatusCode(StatusCodes.Status413PayloadTooLarge);
                memoria.Write(buffer, 0, lidos);
            }
            jpeg = memoria.ToArray();
        }
        catch (BadHttpRequestException ex) // corpo acima do limite (413) ou ilegível
        {
            return StatusCode(ex.StatusCode);
        }
        if (jpeg.Length == 0) return BadRequest(new { erro = "Envie o quadro." });

        DateTime? capturadoEm = null;
        var informada = Request.Headers["X-Capturado-Em"].ToString();
        if (!string.IsNullOrWhiteSpace(informada)
            && DateTimeOffset.TryParse(informada, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var c))
            capturadoEm = c.UtcDateTime;

        try
        {
            var resultado = await _mediator.Send(new RegistrarQuadroAoVivoAgenteCommand(
                agente.Id, segredo, Request.Headers[CabecalhoVersao].ToString(), sessaoId, jpeg, capturadoEm), ct);
            return resultado switch
            {
                ResultadoQuadroAoVivo.Continuar => NoContent(),
                ResultadoQuadroAoVivo.SessaoEncerrada => StatusCode(StatusCodes.Status410Gone, new { codigo = CodigoSessaoEncerrada }),
                ResultadoQuadroAoVivo.LimiteExcedido => StatusCode(StatusCodes.Status429TooManyRequests),
                _ => NaoAutorizado(),
            };
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { codigo = CodigoSessaoNaoEncontrada });
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { erro = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)) });
        }
    }

    [AllowAnonymous]
    [EnableRateLimiting("agente-camera-aovivo")]
    [HttpPost("{agenteId:guid}/ao-vivo/{sessaoId:guid}/falha")]
    [RequestSizeLimit(LimiteCorpoFalhaAoVivoBytes)]
    public async Task<IActionResult> RegistrarFalhaAoVivo(Guid agenteId, Guid sessaoId, CancellationToken ct)
    {
        var segredo = Request.Headers[CabecalhoSegredo].ToString();
        var agente = await _autenticador.ValidarAsync(agenteId, segredo, ct);
        if (agente is null) return NaoAutorizado();

        if (!Request.HasJsonContentType()) return BadRequest(new { erro = "Envie a falha como application/json." });
        FalhaAoVivoRequest? corpo;
        try
        {
            corpo = await Request.ReadFromJsonAsync<FalhaAoVivoRequest>(ct);
        }
        catch (BadHttpRequestException ex)
        {
            return StatusCode(ex.StatusCode);
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(new { erro = "Corpo JSON inválido." });
        }
        if (corpo is null) return BadRequest(new { erro = "Informe a falha." });

        try
        {
            var registrado = await _mediator.Send(new RegistrarFalhaAoVivoAgenteCommand(
                agente.Id, segredo, Request.Headers[CabecalhoVersao].ToString(), sessaoId, corpo.Codigo, corpo.Motivo), ct);
            return registrado ? NoContent() : NaoAutorizado();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { codigo = CodigoSessaoNaoEncontrada });
        }
    }

    private IActionResult NaoAutorizado() => Unauthorized(new { erro = "Agente não autorizado." });
}

public record FalhaAoVivoRequest(string? Codigo, string? Motivo);

public record ResultadoTesteConexaoRequest(bool Sucesso, string? Motivo, string? Detalhe, string? MiniaturaBase64, string? Codigo = null);
