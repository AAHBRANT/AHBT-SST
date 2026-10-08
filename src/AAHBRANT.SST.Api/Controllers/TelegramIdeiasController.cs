using System.Text.Json.Serialization;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Ideias;
using AAHBRANT.SST.Application.Ideias.Commands;
using AAHBRANT.SST.Application.Ideias.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Webhook do bot do Banco de Ideias (especificação §2, §4 e §19: USUÁRIO → TELEGRAM → BOT → IA → BANCO).
// Anônimo por necessidade (o Telegram não envia token Entra ID); a proteção é dupla e falha fechada:
//  1) cabeçalho X-Telegram-Bot-Api-Secret-Token igual a Telegram:IdeiasWebhookSecret;
//  2) chat na lista Telegram:IdeiasChatIds. Mensagens de outros chats são ignoradas em silêncio.
// Registro do webhook (uma vez):
//   curl "https://api.telegram.org/bot<TOKEN>/setWebhook" -d url=<URL-DA-API>/api/telegram/ideias \
//        -d secret_token=<Telegram:IdeiasWebhookSecret> -d allowed_updates='["message"]'
[ApiController]
[AllowAnonymous]
[Route("api/telegram/ideias")]
public class TelegramIdeiasController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITelegramIdeiasService _telegram;
    private readonly ILogger<TelegramIdeiasController> _logger;

    public TelegramIdeiasController(IMediator mediator, ITelegramIdeiasService telegram, ILogger<TelegramIdeiasController> logger)
    {
        _mediator = mediator;
        _telegram = telegram;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receber([FromBody] TelegramUpdate update, CancellationToken ct)
    {
        Request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var segredo);
        if (!_telegram.SegredoValido(segredo.ToString())) return Unauthorized();

        var mensagem = update.Message;
        if (mensagem?.Chat is null || mensagem.From is null || mensagem.From.IsBot) return Ok();
        if (!_telegram.ChatPermitido(mensagem.Chat.Id)) return Ok();

        // Sempre responde 200: o Telegram reenvia o update em qualquer outro status, o que duplicaria ideias.
        try
        {
            await ProcessarAsync(mensagem, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar mensagem do Telegram para o Banco de Ideias.");
            try
            {
                await _telegram.EnviarMensagemAsync(mensagem.Chat.Id,
                    "Não consegui registrar sua ideia agora. Tente novamente em alguns minutos.", mensagem.MessageId, CancellationToken.None);
            }
            catch { /* melhor esforço */ }
        }
        return Ok();
    }

    private async Task ProcessarAsync(TelegramMessage m, CancellationToken ct)
    {
        var texto = m.Text ?? m.Caption ?? string.Empty;
        var nome = string.Join(' ', new[] { m.From!.FirstName, m.From.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(nome)) nome = m.From.Username ?? "Telegram";

        var arquivo = ExtrairArquivo(m);

        // Arquivo sem legenda, enviado como resposta a uma ideia: só anexa.
        if (string.IsNullOrWhiteSpace(texto) && arquivo is not null)
        {
            Guid? alvo = m.ReplyToMessage is null ? null
                : await _mediator.Send(new LocalizarIdeiaPorMensagemTelegramQuery(m.Chat!.Id, m.ReplyToMessage.MessageId), ct);
            if (alvo is null)
            {
                await _telegram.EnviarMensagemAsync(m.Chat!.Id,
                    "Para anexar um arquivo, responda à mensagem da ideia ou envie o arquivo com uma legenda descrevendo a ideia.", m.MessageId, ct);
                return;
            }
            var ok = await AnexarAsync(alvo.Value, arquivo, nome, ct);
            await _telegram.EnviarMensagemAsync(m.Chat!.Id, ok ? "Arquivo anexado à ideia." : "Não consegui anexar o arquivo (limite de 5 MB).", m.MessageId, ct);
            return;
        }

        var resposta = await _mediator.Send(new ProcessarMensagemTelegramIdeiaCommand(
            m.Chat!.Id, m.MessageId, m.ReplyToMessage?.MessageId, m.From.Id, nome, texto), ct);

        if (arquivo is not null && resposta.IdeiaId is { } ideiaId)
            await AnexarAsync(ideiaId, arquivo, nome, ct);

        if (!string.IsNullOrWhiteSpace(resposta.Texto))
        {
            var enviada = await _telegram.EnviarMensagemAsync(m.Chat.Id, resposta.Texto, m.MessageId, ct);
            if (resposta.AguardaResposta && resposta.IdeiaId is { } id && enviada is { } mensagemBot)
                await _mediator.Send(new RegistrarMensagemPerguntaIdeiaCommand(id, mensagemBot), ct);
        }
    }

    private async Task<bool> AnexarAsync(Guid ideiaId, ArquivoTelegram arquivo, string autorNome, CancellationToken ct)
    {
        var bytes = await _telegram.BaixarArquivoAsync(arquivo.FileId, AnexarArquivoIdeiaCommandValidator.TamanhoMaximoBytes, ct);
        if (bytes is null) return false;
        await _mediator.Send(new AnexarArquivoIdeiaCommand(ideiaId, arquivo.Nome, arquivo.ContentType, bytes, new AutorIdeia(null, autorNome)), ct);
        return true;
    }

    private static ArquivoTelegram? ExtrairArquivo(TelegramMessage m)
    {
        if (m.Document is { } d) return new ArquivoTelegram(d.FileId, d.FileName ?? "documento", d.MimeType);
        // Foto: o Telegram manda várias resoluções; a última é a maior.
        if (m.Photo is { Count: > 0 } fotos) return new ArquivoTelegram(fotos[^1].FileId, $"foto-{m.MessageId}.jpg", "image/jpeg");
        return null;
    }

    private record ArquivoTelegram(string FileId, string Nome, string? ContentType);
}

// Subconjunto do Update do Telegram que o Banco de Ideias usa.
public class TelegramUpdate
{
    [JsonPropertyName("message")] public TelegramMessage? Message { get; set; }
}

public class TelegramMessage
{
    [JsonPropertyName("message_id")] public long MessageId { get; set; }
    [JsonPropertyName("from")] public TelegramUser? From { get; set; }
    [JsonPropertyName("chat")] public TelegramChat? Chat { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("caption")] public string? Caption { get; set; }
    [JsonPropertyName("reply_to_message")] public TelegramMessage? ReplyToMessage { get; set; }
    [JsonPropertyName("photo")] public List<TelegramPhotoSize>? Photo { get; set; }
    [JsonPropertyName("document")] public TelegramDocument? Document { get; set; }
}

public class TelegramUser
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("is_bot")] public bool IsBot { get; set; }
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
    [JsonPropertyName("last_name")] public string? LastName { get; set; }
    [JsonPropertyName("username")] public string? Username { get; set; }
}

public class TelegramChat
{
    [JsonPropertyName("id")] public long Id { get; set; }
}

public class TelegramPhotoSize
{
    [JsonPropertyName("file_id")] public string FileId { get; set; } = string.Empty;
}

public class TelegramDocument
{
    [JsonPropertyName("file_id")] public string FileId { get; set; } = string.Empty;
    [JsonPropertyName("file_name")] public string? FileName { get; set; }
    [JsonPropertyName("mime_type")] public string? MimeType { get; set; }
}
