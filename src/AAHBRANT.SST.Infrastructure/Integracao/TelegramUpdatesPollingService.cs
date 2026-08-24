using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao;

// Faz long-polling do getUpdates do Telegram para capturar o "/start <codigo>" que o trabalhador
// manda ao abrir o link de vínculo — bots não podem iniciar uma conversa, só responder a uma
// mensagem recebida (ver GerarVinculoTelegramCommand, que gera o código e o link).
public class TelegramUpdatesPollingService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramOptions _opcoes;
    private readonly ILogger<TelegramUpdatesPollingService> _logger;
    private long _proximoOffset;

    public TelegramUpdatesPollingService(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<TelegramOptions> opcoes,
        ILogger<TelegramUpdatesPollingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.BotToken))
        {
            _logger.LogWarning("Telegram:BotToken não configurado — polling de vínculo do Telegram desativado.");
            return;
        }

        using var cliente = _httpClientFactory.CreateClient();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{_opcoes.BotToken}/getUpdates?timeout=30&offset={_proximoOffset}";
                var resposta = await cliente.GetFromJsonAsync<TelegramGetUpdatesResposta>(url, stoppingToken);

                if (resposta?.Result is { Count: > 0 } atualizacoes)
                {
                    foreach (var atualizacao in atualizacoes)
                    {
                        _proximoOffset = atualizacao.UpdateId + 1;
                        await ProcessarAtualizacaoAsync(atualizacao, stoppingToken);
                        await ProcessarCallbackAsync(atualizacao, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // encerramento normal do host — sai do loop na próxima checagem de stoppingToken
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao consultar getUpdates do Telegram.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessarAtualizacaoAsync(TelegramAtualizacao atualizacao, CancellationToken ct)
    {
        var texto = atualizacao.Message?.Text;
        var chatId = atualizacao.Message?.Chat?.Id;
        if (texto is null || chatId is null || !texto.StartsWith("/start ", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var codigo = texto["/start ".Length..].Trim().ToUpperInvariant();
        if (codigo.Length == 0) return;

        using var escopo = _scopeFactory.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<IAppDbContext>();

        var trabalhador = await db.Trabalhadores.FirstOrDefaultAsync(t => t.TelegramCodigoVinculo == codigo, ct);
        if (trabalhador is null) return;

        trabalhador.TelegramChatId = chatId;
        trabalhador.TelegramVinculadoEm = DateTime.UtcNow;
        trabalhador.TelegramCodigoVinculo = null;
        await db.SaveChangesAsync(ct);
    }

    // Tratamento do clique no botão inline "Confirmo ciência" anexado ao PDF do DDS (ver
    // EnviarDdsTelegramCommandHandler, que gera o callback_data "confirmar:<Guid do envio>").
    private async Task ProcessarCallbackAsync(TelegramAtualizacao atualizacao, CancellationToken ct)
    {
        var callback = atualizacao.CallbackQuery;
        var dados = callback?.Data;
        if (callback is null || dados is null || !dados.StartsWith("confirmar:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var escopo = _scopeFactory.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<IAppDbContext>();
        var telegram = escopo.ServiceProvider.GetRequiredService<ITelegramService>();

        if (!Guid.TryParse(dados["confirmar:".Length..], out var envioId))
        {
            await telegram.ResponderCallbackAsync(callback.Id, "Código inválido.", ct);
            return;
        }

        var envio = await db.DdsTelegramEnvios.FirstOrDefaultAsync(e => e.Id == envioId, ct);
        if (envio is null)
        {
            await telegram.ResponderCallbackAsync(callback.Id, "Envio não encontrado.", ct);
            return;
        }

        if (envio.ConfirmadoEm is null)
        {
            envio.ConfirmadoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var chatId = callback.Message?.Chat?.Id;
            var legendaAtual = callback.Message?.Caption;
            if (chatId is not null && callback.Message?.MessageId is int messageId)
            {
                var novaLegenda = $"{legendaAtual}\n\n✅ Ciência confirmada em {envio.ConfirmadoEm:dd/MM/yyyy HH:mm}";
                await telegram.EditarLegendaAsync(chatId.Value, messageId, novaLegenda, ct);
            }
        }

        await telegram.ResponderCallbackAsync(callback.Id, "Ciência confirmada. Obrigado!", ct);
    }
}

class TelegramGetUpdatesResposta
{
    [JsonPropertyName("result")]
    public List<TelegramAtualizacao> Result { get; set; } = new();
}

class TelegramAtualizacao
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }

    [JsonPropertyName("message")]
    public TelegramMensagem? Message { get; set; }

    [JsonPropertyName("callback_query")]
    public TelegramCallbackQuery? CallbackQuery { get; set; }
}

class TelegramMensagem
{
    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChat? Chat { get; set; }
}

class TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}

class TelegramCallbackQuery
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public string? Data { get; set; }

    [JsonPropertyName("message")]
    public TelegramMensagem? Message { get; set; }
}
