using System.Security.Claims;
using System.Text.Json;
using AAHBRANT.SST.Application.AgenteIa;
using AAHBRANT.SST.Application.SuporteIa;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

/// <summary>
/// SST IA, o botão flutuante de suporte (agente Claude Managed Agents). Mesma permissão do Suporte IA.
/// POST sessoes cria a conversa; POST sessoes/{id}/mensagens responde em text/event-stream;
/// POST transcrever converte a pergunta falada em texto; POST falar lê a resposta com voz neural.
/// </summary>
[ApiController]
[Route("api/agente-ia")]
[Authorize(Policy = "suporte-ia:usar")]
public class AgenteIaController : ControllerBase
{
    private const int TamanhoMaximoMensagem = 4000;
    // Pergunta falada é curta; 10 MB cobre alguns minutos de opus com folga.
    private const long TamanhoMaximoAudio = 10_000_000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAgenteSuporteIa _agente;
    private readonly ITranscricaoAudioService _transcricao;
    private readonly ISinteseVoz _voz;
    private readonly ILogger<AgenteIaController> _logger;

    // Uma resposta comum tem ~1.000 caracteres; acima disso o app lê só o começo.
    private const int TamanhoMaximoFala = 5000;

    public AgenteIaController(IAgenteSuporteIa agente, ITranscricaoAudioService transcricao, ISinteseVoz voz, ILogger<AgenteIaController> logger)
    {
        _agente = agente;
        _transcricao = transcricao;
        _voz = voz;
        _logger = logger;
    }

    // Lê a resposta em voz alta (voz neural do Azure). O áudio não é salvo. 503 = o app usa a voz do aparelho.
    [HttpPost("falar")]
    public async Task<IActionResult> Falar([FromBody] FalarRespostaBody body, CancellationToken ct)
    {
        if (!_voz.Configurado)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { erro = "Voz neural não configurada neste ambiente." });
        var texto = body.Texto?.Trim() ?? "";
        if (texto.Length == 0)
            return BadRequest(new { erro = "Texto vazio." });
        if (texto.Length > TamanhoMaximoFala)
            texto = texto[..TamanhoMaximoFala];

        try
        {
            var audio = await _voz.SintetizarAsync(texto, ct);
            Response.Headers.CacheControl = "no-store";
            return File(audio, "audio/mpeg");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha ao gerar a voz do SST IA");
            return StatusCode(StatusCodes.Status502BadGateway, new { erro = "Não foi possível gerar a voz agora." });
        }
    }

    // Só transcreve (o áudio não é salvo).
    [HttpPost("transcrever")]
    [RequestSizeLimit(TamanhoMaximoAudio + 1_000_000)]
    public async Task<IActionResult> Transcrever([FromForm] TranscreverPerguntaBody body, CancellationToken ct)
    {
        if (!_transcricao.Configurado)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { erro = "Transcrição de áudio ainda não está configurada neste ambiente." });
        if (body.Audio is null || body.Audio.Length == 0)
            return BadRequest(new { erro = "Nenhum áudio recebido." });
        if (body.Audio.Length > TamanhoMaximoAudio)
            return BadRequest(new { erro = "Pergunta muito longa. Grave até cerca de 5 minutos." });

        await using var stream = body.Audio.OpenReadStream();
        var nomeArquivo = string.IsNullOrWhiteSpace(body.Audio.FileName) ? "audio.webm" : body.Audio.FileName;
        var transcricao = await _transcricao.TranscreverAsync(stream, nomeArquivo, body.Audio.ContentType, ct);
        return Ok(new { transcricao = transcricao?.Trim() ?? "" });
    }

    [HttpPost("sessoes")]
    public async Task<IActionResult> CriarSessao(CancellationToken ct)
    {
        if (!_agente.Configurado)
            return Problem("Assistente de IA não configurado.", statusCode: StatusCodes.Status503ServiceUnavailable);

        try
        {
            var id = await _agente.CriarSessaoAsync(ChaveUsuario(), ct);
            return Ok(new { sessaoId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar sessão do agente de suporte");
            return Problem("Não foi possível iniciar o assistente agora.", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpPost("sessoes/{sessaoId}/mensagens")]
    public async Task EnviarMensagem(string sessaoId, [FromBody] EnviarMensagemAgenteBody body, CancellationToken ct)
    {
        var texto = body.Texto?.Trim() ?? "";
        if (texto.Length == 0 || texto.Length > TamanhoMaximoMensagem)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { erro = $"Mensagem vazia ou acima de {TamanhoMaximoMensagem} caracteres." }, ct);
            return;
        }
        if (!_agente.Configurado)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }
        if (!sessaoId.StartsWith("sesn_", StringComparison.Ordinal)
            || !await _agente.SessaoPertenceAsync(sessaoId, ChaveUsuario(), ct))
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Tela atual vai junto como contexto (o agente não acessa dados do sistema).
        var tela = body.Tela?.Trim();
        if (!string.IsNullOrEmpty(tela))
            texto = $"[Tela atual do usuário no G-SST: {tela[..Math.Min(tela.Length, 200)]}]\n\n{texto}";

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var evento in _agente.EnviarAsync(sessaoId, texto, ct))
        {
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(evento, Json)}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    // Dono da sessão: objectId do Entra (ou e-mail). Sem login (dev com auth desligada) = "anonimo".
    private string ChaveUsuario() =>
        User.FindFirst("oid")?.Value
        ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
        ?? User.FindFirst("preferred_username")?.Value
        ?? User.FindFirst(ClaimTypes.Email)?.Value
        ?? "anonimo";
}

public record EnviarMensagemAgenteBody(string? Texto, string? Tela);

public record FalarRespostaBody(string? Texto);

public class TranscreverPerguntaBody
{
    public IFormFile? Audio { get; set; }
}
