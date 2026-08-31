using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AAHBRANT.SST.Application.Calendario;
using AAHBRANT.SST.Application.Common.Interfaces;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Teams;

// Integração do Motor de Alertas com o Calendário do Teams (docs/superpowers/specs/
// 2026-08-28-calendario-teams-design.md) — cria/atualiza/cancela eventos via Microsoft Graph
// (/users/{aadObjectId}/events), reaproveitando o mesmo App Registration/GraphOptions já usado pelo
// Activity Feed (GraphActivityNotificacaoTeamsService). Todo evento é de dia inteiro (isAllDay=true,
// showAs=free) — a origem só tem uma data de vencimento, nunca um horário.
//
// Mesmo princípio do Activity Feed: lança exceção em vez de engolir a falha — sempre chamado por um
// consumidor da fila de retry (ver IFilaCalendarioTeams), que decide o que fazer com o erro.
public class GraphCalendarioTeamsService : ICalendarioTeamsService
{
    private readonly IAppDbContext _db;
    private readonly GraphOptions _opcoes;
    private readonly IHttpClientFactory _httpClientFactory;

    public GraphCalendarioTeamsService(
        IAppDbContext db, IOptions<GraphOptions> opcoes, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _opcoes = opcoes.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> CriarEventoAsync(
        Guid organizadorUsuarioId, string titulo, string? descricao, DateTime data, CancellationToken ct = default)
    {
        var (httpClient, aadObjectId) = await PrepararRequisicaoAsync(organizadorUsuarioId, ct);

        using var requisicao = new HttpRequestMessage(
            HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{aadObjectId}/events")
        {
            Content = JsonContent.Create(MontarEventoDiaInteiro(titulo, descricao, data)),
        };

        var resposta = await httpClient.SendAsync(requisicao, ct);
        await GarantirSucessoAsync(resposta, organizadorUsuarioId, ct);

        var corpoResposta = await resposta.Content.ReadFromJsonAsync<GraphEventoResposta>(cancellationToken: ct);
        return corpoResposta?.Id
            ?? throw new InvalidOperationException(
                $"Graph não retornou o id do evento criado para o usuário {organizadorUsuarioId}.");
    }

    public async Task AtualizarEventoAsync(
        Guid organizadorUsuarioId, string graphEventId, string titulo, string? descricao, DateTime data,
        CancellationToken ct = default)
    {
        var (httpClient, aadObjectId) = await PrepararRequisicaoAsync(organizadorUsuarioId, ct);

        using var requisicao = new HttpRequestMessage(
            HttpMethod.Patch, $"https://graph.microsoft.com/v1.0/users/{aadObjectId}/events/{graphEventId}")
        {
            Content = JsonContent.Create(MontarEventoDiaInteiro(titulo, descricao, data)),
        };

        var resposta = await httpClient.SendAsync(requisicao, ct);
        await GarantirSucessoAsync(resposta, organizadorUsuarioId, ct);
    }

    public async Task CancelarEventoAsync(Guid organizadorUsuarioId, string graphEventId, CancellationToken ct = default)
    {
        var (httpClient, aadObjectId) = await PrepararRequisicaoAsync(organizadorUsuarioId, ct);

        using var requisicao = new HttpRequestMessage(
            HttpMethod.Delete, $"https://graph.microsoft.com/v1.0/users/{aadObjectId}/events/{graphEventId}");

        var resposta = await httpClient.SendAsync(requisicao, ct);
        await GarantirSucessoAsync(resposta, organizadorUsuarioId, ct);
    }

    // GET /users/{aadObjectId}/calendarView — lê os eventos reais do Outlook/Teams do usuário no
    // intervalo (requisito do usuário, 2026-08-29: "quero o calendário do Teams dentro do
    // aplicativo"). Reaproveita a mesma permissão de aplicativo (Calendars.ReadWrite) e o mesmo
    // App Registration já usados por CriarEventoAsync/AtualizarEventoAsync/CancelarEventoAsync
    // acima — não é preciso nenhuma permissão nova nem fluxo delegado (on-behalf-of).
    public async Task<IReadOnlyList<EventoGraphDto>> ListarEventosAsync(
        Guid usuarioId, DateTime inicio, DateTime fim, CancellationToken ct = default)
    {
        var (httpClient, aadObjectId) = await PrepararRequisicaoAsync(usuarioId, ct);

        var inicioStr = Uri.EscapeDataString(inicio.ToString("yyyy-MM-ddTHH:mm:ss"));
        var fimStr = Uri.EscapeDataString(fim.ToString("yyyy-MM-ddTHH:mm:ss"));
        using var requisicao = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://graph.microsoft.com/v1.0/users/{aadObjectId}/calendarView" +
            $"?startDateTime={inicioStr}&endDateTime={fimStr}&$orderby=start/dateTime&$top=250");
        requisicao.Headers.Add("Prefer", "outlook.timezone=\"America/Sao_Paulo\"");

        var resposta = await httpClient.SendAsync(requisicao, ct);
        await GarantirSucessoAsync(resposta, usuarioId, ct);

        var corpoResposta = await resposta.Content.ReadFromJsonAsync<GraphCalendarViewResposta>(cancellationToken: ct);
        return corpoResposta?.Value?.Select(MapearEvento).ToList() ?? new List<EventoGraphDto>();
    }

    private static EventoGraphDto MapearEvento(GraphEventoView evento) => new(
        evento.Id ?? string.Empty,
        evento.Subject ?? "(sem assunto)",
        ParseDataHora(evento.Start),
        ParseDataHora(evento.End),
        evento.IsAllDay,
        evento.Location?.DisplayName,
        evento.Organizer?.EmailAddress?.Name,
        evento.IsOnlineMeeting,
        evento.OnlineMeeting?.JoinUrl);

    private static DateTime ParseDataHora(GraphDataHora? dataHora) =>
        dataHora is not null && DateTime.TryParse(dataHora.DateTime, out var valor) ? valor : default;

    private async Task<(HttpClient httpClient, string aadObjectId)> PrepararRequisicaoAsync(
        Guid organizadorUsuarioId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.ClientSecret))
            throw new InvalidOperationException(
                "Graph:ClientSecret não configurado — permissão Calendars.ReadWrite do Entra ID ainda não provisionada.");

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == organizadorUsuarioId, ct);
        if (usuario is null || string.IsNullOrWhiteSpace(usuario.AzureAdObjectId))
            throw new InvalidOperationException(
                $"Usuário {organizadorUsuarioId} não possui AzureAdObjectId cadastrado — não é possível sincronizar o calendário.");

        var credential = new ClientSecretCredential(_opcoes.TenantId, _opcoes.ClientId, _opcoes.ClientSecret);
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }), ct);

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return (httpClient, usuario.AzureAdObjectId);
    }

    // internal (não private) só para permitir teste direto do cálculo de fronteira de dia e do
    // formato do payload — ver GraphCalendarioTeamsServicePayloadTests. InternalsVisibleTo em
    // AssemblyInfo.cs restringe o acesso a AAHBRANT.SST.Application.Tests.
    internal static object MontarEventoDiaInteiro(string titulo, string? descricao, DateTime data)
    {
        var inicio = data.Date;
        var fim = inicio.AddDays(1);

        return new
        {
            subject = titulo,
            body = new { contentType = "text", content = descricao ?? string.Empty },
            start = new { dateTime = inicio.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "America/Sao_Paulo" },
            end = new { dateTime = fim.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "America/Sao_Paulo" },
            isAllDay = true,
            showAs = "free",
        };
    }

    private static async Task GarantirSucessoAsync(HttpResponseMessage resposta, Guid organizadorUsuarioId, CancellationToken ct)
    {
        if (resposta.IsSuccessStatusCode)
            return;

        var corpoResposta = await resposta.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"Falha ao sincronizar evento de calendário no Teams para o usuário {organizadorUsuarioId}: " +
            $"{(int)resposta.StatusCode} {resposta.StatusCode} — {corpoResposta}");
    }

    private class GraphEventoResposta
    {
        public string? Id { get; set; }
    }

    private class GraphCalendarViewResposta
    {
        [JsonPropertyName("value")]
        public List<GraphEventoView>? Value { get; set; }
    }

    private class GraphEventoView
    {
        public string? Id { get; set; }
        public string? Subject { get; set; }
        public GraphDataHora? Start { get; set; }
        public GraphDataHora? End { get; set; }
        public bool IsAllDay { get; set; }
        public bool IsOnlineMeeting { get; set; }
        public GraphLocal? Location { get; set; }
        public GraphOrganizador? Organizer { get; set; }
        public GraphOnlineMeeting? OnlineMeeting { get; set; }
    }

    private class GraphDataHora
    {
        public string? DateTime { get; set; }
        public string? TimeZone { get; set; }
    }

    private class GraphLocal
    {
        public string? DisplayName { get; set; }
    }

    private class GraphOrganizador
    {
        public GraphEmailAddress? EmailAddress { get; set; }
    }

    private class GraphEmailAddress
    {
        public string? Name { get; set; }
    }

    private class GraphOnlineMeeting
    {
        public string? JoinUrl { get; set; }
    }
}
