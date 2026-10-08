using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Telegram;

public class TelegramIdeiasService : ITelegramIdeiasService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<TelegramSuporteOptions> _options;
    private readonly ILogger<TelegramIdeiasService> _logger;

    public TelegramIdeiasService(
        IHttpClientFactory httpClientFactory,
        IOptions<TelegramSuporteOptions> options,
        ILogger<TelegramIdeiasService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public bool ChatPermitido(long chatId)
    {
        var lista = _options.Value.IdeiasChatIds;
        if (string.IsNullOrWhiteSpace(lista)) return false;
        return lista.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Any(x => long.TryParse(x, out var id) && id == chatId);
    }

    public bool SegredoValido(string? segredoRecebido)
    {
        var esperado = _options.Value.IdeiasWebhookSecret;
        if (string.IsNullOrWhiteSpace(esperado) || string.IsNullOrEmpty(segredoRecebido)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(esperado), Encoding.UTF8.GetBytes(segredoRecebido));
    }

    public async Task<long?> EnviarMensagemAsync(long chatId, string texto, long? respondeMensagemId, CancellationToken ct = default)
    {
        var token = _options.Value.BotToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Telegram não configurado (Telegram:BotToken) — resposta do Banco de Ideias não enviada.");
            return null;
        }

        var client = _httpClientFactory.CreateClient();
        var corpo = new Dictionary<string, object>
        {
            ["chat_id"] = chatId,
            ["text"] = texto.Length > 3900 ? texto[..3900] : texto
        };
        if (respondeMensagemId is { } respondida) corpo["reply_to_message_id"] = respondida;

        var response = await client.PostAsJsonAsync($"https://api.telegram.org/bot{token}/sendMessage", corpo, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Falha ao responder no Telegram (Banco de Ideias): {StatusCode}", response.StatusCode);
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.TryGetProperty("result", out var result)
            && result.TryGetProperty("message_id", out var id) && id.TryGetInt64(out var messageId)
            ? messageId : null;
    }

    public async Task<byte[]?> BaixarArquivoAsync(string fileId, int maxBytes, CancellationToken ct = default)
    {
        var token = _options.Value.BotToken;
        if (string.IsNullOrWhiteSpace(token)) return null;

        var client = _httpClientFactory.CreateClient();
        var meta = await client.GetAsync($"https://api.telegram.org/bot{token}/getFile?file_id={Uri.EscapeDataString(fileId)}", ct);
        if (!meta.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await meta.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("result", out var result)) return null;
        if (result.TryGetProperty("file_size", out var tamanho) && tamanho.TryGetInt64(out var bytes) && bytes > maxBytes) return null;
        if (!result.TryGetProperty("file_path", out var caminho) || caminho.GetString() is not { } filePath) return null;

        var arquivo = await client.GetAsync($"https://api.telegram.org/file/bot{token}/{filePath}", ct);
        if (!arquivo.IsSuccessStatusCode) return null;
        var conteudo = await arquivo.Content.ReadAsByteArrayAsync(ct);
        return conteudo.Length > maxBytes ? null : conteudo;
    }
}
