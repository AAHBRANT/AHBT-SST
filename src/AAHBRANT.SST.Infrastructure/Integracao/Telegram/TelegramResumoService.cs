using System.Net.Http.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Telegram;

// Mesmo bot e, por padrão, o mesmo grupo do Suporte IA (Telegram:BotToken e Telegram:SuporteChatId, decisão
// do usuário em 07/10). Se um dia quiser separar, basta preencher Telegram:ResumoChatId. O token e o chat
// são segredos de configuração do ambiente; sem eles o envio é ignorado.
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

    public async Task<bool> EnviarImagemAsync(byte[] imagemPng, string legenda, CancellationToken ct = default)
    {
        var token = _options.Value.BotToken;
        var chatId = ObterChatId();
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogInformation("Telegram de resumos não configurado (Telegram:BotToken e Telegram:SuporteChatId ou ResumoChatId): imagem não enviada.");
            return false;
        }

        using var corpo = new MultipartFormDataContent();
        corpo.Add(new StringContent(chatId), "chat_id");
        corpo.Add(new StringContent(legenda.Length > 1000 ? legenda[..1000] : legenda), "caption");
        var foto = new ByteArrayContent(imagemPng);
        foto.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        corpo.Add(foto, "photo", "resumo-dds.png");

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync($"https://api.telegram.org/bot{token}/sendPhoto", corpo, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Falha ao enviar a imagem do resumo ao Telegram: {StatusCode}", response.StatusCode);
            return false;
        }
        return true;
    }

    public async Task<bool> EnviarDocumentoAsync(byte[] arquivo, string nomeArquivo, string legenda, CancellationToken ct = default)
    {
        var token = _options.Value.BotToken;
        var chatId = ObterChatId();
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogInformation("Telegram de resumos não configurado (Telegram:BotToken e Telegram:SuporteChatId ou ResumoChatId): documento não enviado.");
            return false;
        }

        using var corpo = new MultipartFormDataContent();
        corpo.Add(new StringContent(chatId), "chat_id");
        corpo.Add(new StringContent(legenda.Length > 1000 ? legenda[..1000] : legenda), "caption");
        var documento = new ByteArrayContent(arquivo);
        documento.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        corpo.Add(documento, "document", nomeArquivo);

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync($"https://api.telegram.org/bot{token}/sendDocument", corpo, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Falha ao enviar o documento do relatório ao Telegram: {StatusCode}", response.StatusCode);
            return false;
        }
        return true;
    }

    private string? ObterChatId() =>
        string.IsNullOrWhiteSpace(_options.Value.ResumoChatId) ? _options.Value.SuporteChatId : _options.Value.ResumoChatId;

    public async Task EnviarAsync(string mensagem, CancellationToken ct = default)
    {
        var token = _options.Value.BotToken;
        var chatId = ObterChatId();

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogInformation("Telegram de resumos não configurado (Telegram:BotToken e Telegram:SuporteChatId ou ResumoChatId): resumo não enviado.");
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
