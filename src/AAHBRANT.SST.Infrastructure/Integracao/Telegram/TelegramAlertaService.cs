using System.Net.Http.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Telegram;

// Mesmo bot do suporte (Telegram:BotToken), mas para o chat de alertas técnicos (Telegram:AlertasChatId,
// ou o SuporteChatId quando vazio). Texto puro, sem formatação: a mensagem vem do agente e não deve
// conseguir quebrar o envio com um caractere de Markdown.
public class TelegramAlertaService : ITelegramAlertaService
{
    public const int TamanhoMaximo = 3900;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<TelegramSuporteOptions> _options;
    private readonly ILogger<TelegramAlertaService> _logger;

    public TelegramAlertaService(IHttpClientFactory httpClientFactory, IOptions<TelegramSuporteOptions> options, ILogger<TelegramAlertaService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> EnviarAsync(string mensagem, CancellationToken ct = default)
    {
        var token = _options.Value.BotToken;
        var chatId = string.IsNullOrWhiteSpace(_options.Value.AlertasChatId) ? _options.Value.SuporteChatId : _options.Value.AlertasChatId;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogWarning("Alerta de engenharia não enviado: configure Telegram:BotToken e Telegram:AlertasChatId (ou SuporteChatId).");
            return false;
        }

        var texto = mensagem.Length > TamanhoMaximo ? mensagem[..TamanhoMaximo] + "…" : mensagem;
        var cliente = _httpClientFactory.CreateClient();
        using var resposta = await cliente.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new { chat_id = chatId, text = texto, disable_web_page_preview = true },
            ct);
        if (resposta.IsSuccessStatusCode)
            return true;

        _logger.LogWarning("Telegram recusou o alerta de engenharia: {StatusCode} {Erro}", resposta.StatusCode, await resposta.Content.ReadAsStringAsync(ct));
        return false;
    }
}
