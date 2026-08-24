using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao;

public class TelegramBotService : ITelegramService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelegramOptions _opcoes;

    public TelegramBotService(IHttpClientFactory httpClientFactory, IOptions<TelegramOptions> opcoes)
    {
        _httpClientFactory = httpClientFactory;
        _opcoes = opcoes.Value;
    }

    public string ObterNomeUsuarioBot()
    {
        if (string.IsNullOrWhiteSpace(_opcoes.BotUsername))
            throw new InvalidOperationException("Telegram:BotUsername não configurado em appsettings.");
        return _opcoes.BotUsername;
    }

    public async Task<int?> EnviarDocumentoAsync(
        long chatId,
        string nomeArquivo,
        byte[] conteudo,
        string? legenda,
        string? callbackData,
        CancellationToken ct)
    {
        VerificarToken();
        using var cliente = _httpClientFactory.CreateClient();
        using var corpo = new MultipartFormDataContent { { new StringContent(chatId.ToString()), "chat_id" } };
        if (!string.IsNullOrWhiteSpace(legenda)) corpo.Add(new StringContent(legenda), "caption");
        if (!string.IsNullOrWhiteSpace(callbackData))
        {
            var teclado = new
            {
                inline_keyboard = new[]
                {
                    new[] { new { text = "✅ Confirmo ciência", callback_data = callbackData } },
                },
            };
            corpo.Add(new StringContent(JsonSerializer.Serialize(teclado)), "reply_markup");
        }
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        corpo.Add(arquivo, "document", nomeArquivo);

        var resposta = await cliente.PostAsync($"https://api.telegram.org/bot{_opcoes.BotToken}/sendDocument", corpo, ct);
        resposta.EnsureSuccessStatusCode();
        var envelope = await resposta.Content.ReadFromJsonAsync<TelegramEnvioResposta>(cancellationToken: ct);
        return envelope?.Result?.MessageId;
    }

    public async Task ResponderCallbackAsync(string callbackQueryId, string texto, CancellationToken ct)
    {
        VerificarToken();
        using var cliente = _httpClientFactory.CreateClient();
        var payload = new { callback_query_id = callbackQueryId, text = texto };
        var resposta = await cliente.PostAsJsonAsync(
            $"https://api.telegram.org/bot{_opcoes.BotToken}/answerCallbackQuery", payload, ct);
        resposta.EnsureSuccessStatusCode();
    }

    public async Task EditarLegendaAsync(long chatId, int messageId, string legenda, CancellationToken ct)
    {
        VerificarToken();
        using var cliente = _httpClientFactory.CreateClient();
        var payload = new { chat_id = chatId, message_id = messageId, caption = legenda };
        var resposta = await cliente.PostAsJsonAsync(
            $"https://api.telegram.org/bot{_opcoes.BotToken}/editMessageCaption", payload, ct);
        resposta.EnsureSuccessStatusCode();
    }

    private void VerificarToken()
    {
        if (string.IsNullOrWhiteSpace(_opcoes.BotToken))
            throw new InvalidOperationException("Telegram:BotToken não configurado em appsettings.");
    }

    private class TelegramEnvioResposta
    {
        [JsonPropertyName("result")]
        public TelegramEnvioResultado? Result { get; set; }
    }

    private class TelegramEnvioResultado
    {
        [JsonPropertyName("message_id")]
        public int MessageId { get; set; }
    }
}
