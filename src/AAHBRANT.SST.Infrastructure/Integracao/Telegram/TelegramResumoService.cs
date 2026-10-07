using System.Net.Http.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Telegram;

// Mesmo bot do Suporte IA (Telegram:BotToken), mas chat separado (Telegram:ResumoChatId). O token e o
// chat são segredos de configuração do ambiente; sem eles o envio é ignorado.
public class TelegramResumoService : ITelegramResumoService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<TelegramSuporteOptions> _options;
    private readonly ILogger<TelegramResumoService> _logger;

    public TelegramResumoService(
        IHttpClientFactory httpClientFactory,
        IOptions<TelegramSuporteOptions> options,
        ILogger<TelegramResumoService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task EnviarAsync(string mensagem, CancellationToken ct = default)
    {
        var token = _options.Value.BotToken;
        var chatId = _options.Value.ResumoChatId;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogInformation("Telegram de resumos não configurado (Telegram:BotToken e Telegram:ResumoChatId): resumo não enviado.");
            return;
        }

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new { chat_id = chatId, text = mensagem.Length > 3900 ? mensagem[..3900] : mensagem },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            // Nunca registra o corpo da mensagem (pode ter dado de funcionário); só o status.
            _logger.LogWarning("Falha ao enviar resumo ao Telegram: {StatusCode}", response.StatusCode);
        }
    }
}
