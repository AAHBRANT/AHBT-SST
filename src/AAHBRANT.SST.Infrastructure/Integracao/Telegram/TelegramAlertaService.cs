using System.Net.Http.Json;
using System.Text.Json;
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

    private string? Token => _options.Value.BotToken;

    private string? ChatId => string.IsNullOrWhiteSpace(_options.Value.AlertasChatId) ? _options.Value.SuporteChatId : _options.Value.AlertasChatId;

    private static string Cortar(string mensagem) => mensagem.Length > TamanhoMaximo ? mensagem[..TamanhoMaximo] + "…" : mensagem;

    public async Task<bool> EnviarAsync(string mensagem, CancellationToken ct = default)
        => await EnviarInternoAsync(mensagem, null, ct) is not null;

    public Task<long?> EnviarComBotoesAsync(string mensagem, IReadOnlyList<BotaoTelegram> botoes, CancellationToken ct = default)
        => EnviarInternoAsync(mensagem, new { inline_keyboard = new[] { botoes.Select(b => new { text = b.Texto, callback_data = b.Dado }).ToArray() } }, ct);

    private async Task<long?> EnviarInternoAsync(string mensagem, object? teclado, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Token) || string.IsNullOrWhiteSpace(ChatId))
        {
            _logger.LogWarning("Alerta de engenharia não enviado: configure Telegram:BotToken e Telegram:AlertasChatId (ou SuporteChatId).");
            return null;
        }

        var corpo = new Dictionary<string, object> { ["chat_id"] = ChatId!, ["text"] = Cortar(mensagem), ["disable_web_page_preview"] = true };
        if (teclado is not null)
            corpo["reply_markup"] = teclado;

        using var resposta = await ChamarAsync("sendMessage", corpo, ct);
        if (resposta is null)
            return null;
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(ct));
        return json.RootElement.TryGetProperty("result", out var resultado) && resultado.TryGetProperty("message_id", out var id)
            ? id.GetInt64()
            : 0;
    }

    public async Task EditarAsync(long mensagemId, string mensagem, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Token) || string.IsNullOrWhiteSpace(ChatId))
            return;
        // Sem reply_markup: o Telegram remove os botões da mensagem.
        using var _ = await ChamarAsync("editMessageText",
            new Dictionary<string, object> { ["chat_id"] = ChatId!, ["message_id"] = mensagemId, ["text"] = Cortar(mensagem) }, ct);
    }

    public async Task ResponderCliqueAsync(string cliqueId, string aviso, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Token))
            return;
        using var _ = await ChamarAsync("answerCallbackQuery",
            new Dictionary<string, object> { ["callback_query_id"] = cliqueId, ["text"] = aviso.Length > 190 ? aviso[..190] : aviso }, ct);
    }

    // Em conversa privada, o id do chat é o id do próprio usuário.
    public bool UsuarioAutorizado(long usuarioTelegramId)
        => long.TryParse(ChatId, out var chat) && chat > 0 && chat == usuarioTelegramId;

    public async Task<bool> RegistrarWebhookAsync(string url, string segredo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Token))
            return false;
        using var resposta = await ChamarAsync("setWebhook", new Dictionary<string, object>
        {
            ["url"] = url,
            ["secret_token"] = segredo,
            ["allowed_updates"] = new[] { "callback_query" },
            ["drop_pending_updates"] = true,
        }, ct);
        return resposta is not null;
    }

    // Devolve a resposta só quando o Telegram aceitou; o motivo da recusa vai para o log (nunca o token).
    private async Task<HttpResponseMessage?> ChamarAsync(string metodo, object corpo, CancellationToken ct)
    {
        var cliente = _httpClientFactory.CreateClient();
        var resposta = await cliente.PostAsJsonAsync($"https://api.telegram.org/bot{Token}/{metodo}", corpo, ct);
        if (resposta.IsSuccessStatusCode)
            return resposta;
        _logger.LogWarning("Telegram recusou {Metodo}: {StatusCode} {Erro}", metodo, resposta.StatusCode, await resposta.Content.ReadAsStringAsync(ct));
        resposta.Dispose();
        return null;
    }
}
