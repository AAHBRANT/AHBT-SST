using System.Net.Http.Headers;
using System.Net.Http.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Teams;

// Motor Central de Alertas, Etapa 4 — envia a notificação de "sino" do Teams (Activity Feed) para o
// Usuario via Microsoft Graph (POST /users/{aadObjectId}/teamwork/sendActivityNotification), sem Bot
// Framework, sem Bot Channels Registration e sem precisar de uma ConversationReference capturada
// antecipadamente. Nunca é chamado diretamente pelo fluxo que cria o Alerta (ver IFilaNotificacaoTeams)
// — sempre por um consumidor da fila de retry, que decide o que fazer com a exceção lançada aqui em
// caso de falha.
public class GraphActivityNotificacaoTeamsService : INotificacaoTeamsService
{
    private const int TamanhoMaximoPreviewText = 80;

    private readonly IAppDbContext _db;
    private readonly GraphOptions _opcoes;
    private readonly IHttpClientFactory _httpClientFactory;

    public GraphActivityNotificacaoTeamsService(
        IAppDbContext db, IOptions<GraphOptions> opcoes, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _opcoes = opcoes.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> EnviarAsync(Guid usuarioId, string titulo, string? descricao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.ClientSecret))
            throw new InvalidOperationException(
                "Graph:ClientSecret não configurado — permissão TeamsActivity.Send do Entra ID ainda não provisionada.");

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null || string.IsNullOrWhiteSpace(usuario.AzureAdObjectId))
            throw new InvalidOperationException(
                $"Usuário {usuarioId} não possui AzureAdObjectId cadastrado — não é possível enviar notificação no Teams.");

        var credential = new ClientSecretCredential(_opcoes.TenantId, _opcoes.ClientId, _opcoes.ClientSecret);
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }), ct);

        var previewText = titulo.Length > TamanhoMaximoPreviewText
            ? titulo[..TamanhoMaximoPreviewText]
            : titulo;

        var corpo = new
        {
            topic = new { source = "text", value = "Alerta SST" },
            activityType = _opcoes.ActivityType,
            previewText = new { content = previewText },
            templateParameters = new[]
            {
                new { name = "titulo", value = titulo },
                new { name = "descricao", value = descricao ?? string.Empty },
            },
        };

        var httpClient = _httpClientFactory.CreateClient();
        using var requisicao = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/users/{usuario.AzureAdObjectId}/teamwork/sendActivityNotification")
        {
            Content = JsonContent.Create(corpo),
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var resposta = await httpClient.SendAsync(requisicao, ct);
        if (!resposta.IsSuccessStatusCode)
        {
            var corpoResposta = await resposta.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Falha ao enviar notificação de Activity Feed no Teams para o usuário {usuarioId}: " +
                $"{(int)resposta.StatusCode} {resposta.StatusCode} — {corpoResposta}");
        }

        return true;
    }
}
