using System.Net.Http.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Telegram;

public class TelegramSuporteOptions
{
    public string? BotToken { get; set; }
    public string? SuporteChatId { get; set; }
    // Opcional: chat próprio para resumos operacionais (ex.: resumo do DDS ao encerrar). Vazio = usa SuporteChatId.
    public string? ResumoChatId { get; set; }
    // Banco de Ideias: chats do Telegram autorizados a registrar ideias (ids separados por vírgula) e
    // segredo do webhook (setWebhook secret_token). Sem os dois, o webhook rejeita tudo (falha fechada).
    public string? IdeiasChatIds { get; set; }
    public string? IdeiasWebhookSecret { get; set; }
}

public class TelegramSuporteService : ITelegramSuporteService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<TelegramSuporteOptions> _options;
    private readonly ILogger<TelegramSuporteService> _logger;

    public TelegramSuporteService(
        IHttpClientFactory httpClientFactory,
        IOptions<TelegramSuporteOptions> options,
        ILogger<TelegramSuporteService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task EnviarDemandaAsync(string mensagem, CancellationToken ct = default)
    {
        var token = _options.Value.BotToken;
        var chatId = _options.Value.SuporteChatId;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogWarning("Telegram de suporte IA não configurado. Configure Telegram:BotToken e Telegram:SuporteChatId.");
            return;
        }

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new
            {
                chat_id = chatId,
                text = mensagem.Length > 3900 ? mensagem[..3900] : mensagem
            },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var erro = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Falha ao enviar demanda de suporte IA ao Telegram: {StatusCode} {Erro}", response.StatusCode, erro);
        }
    }
}
