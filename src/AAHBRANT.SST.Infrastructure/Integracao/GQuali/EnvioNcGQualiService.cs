using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.GQuali;

// Credenciais/endpoint da integração de NC com o G-QUALI (seção "GQuali", mesmo padrão de GrhOptions).
// Vazio até a integração ser provisionada: o serviço não envia nada. LinkNcBase (opcional) = endereço da web
// do G-SST, para o G-QUALI mostrar "Abrir no G-SST".
public class GQualiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string LinkNcBase { get; set; } = string.Empty;

    public bool Configurada => new[] { BaseUrl, TenantId, ClientId, ClientSecret, Scope }.All(v => !string.IsNullOrWhiteSpace(v));
}

// Envia a fila de saída das NCs ao G-QUALI (POST /api/integracoes/ncs/eventos). Pode rodar em mais de uma
// réplica: cada evento é reservado (Pendente → Enviando) por UPDATE condicional antes do envio. 2xx → Enviado;
// 400 → Recusado (não repete; erro guardado); 401/403/409/429/5xx/rede → volta a Pendente com espera crescente.
// Reserva esquecida (processo caiu) volta a valer depois de 5 minutos.
public class EnvioNcGQualiService(IServiceScopeFactory escopos, IOptions<GQualiOptions> opcoes,
    IHttpClientFactory http, ILogger<EnvioNcGQualiService> log) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan ReservaExpira = TimeSpan.FromMinutes(5);
    private const int Lote = 20;
    private ClientSecretCredential? _credencial;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!opcoes.Value.Configurada)
        {
            log.LogInformation("Integração de NC com o G-QUALI não configurada (seção GQuali): envio desligado.");
            return;
        }
        while (!ct.IsCancellationRequested)
        {
            try { await ProcessarLoteAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Falha no envio de NC ao G-QUALI."); }
            await Task.Delay(Intervalo, ct);
        }
    }

    public async Task<int> ProcessarLoteAsync(CancellationToken ct)
    {
        using var escopo = escopos.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<SstDbContext>();
        var agora = DateTime.UtcNow;
        var expirou = agora - ReservaExpira;
        var candidatos = await db.EventosNcGQuali.AsNoTracking()
            .Where(e => (e.Situacao == SituacaoEventoNcGQuali.Pendente && e.ProximaTentativaEmUtc <= agora)
                || (e.Situacao == SituacaoEventoNcGQuali.Enviando && e.EnviandoDesdeUtc < expirou))
            .OrderBy(e => e.Revisao).Take(Lote).Select(e => e.Id).ToListAsync(ct);
        var enviados = 0;
        foreach (var id in candidatos)
        {
            var reservado = await db.EventosNcGQuali
                .Where(e => e.Id == id && (e.Situacao == SituacaoEventoNcGQuali.Pendente
                    || (e.Situacao == SituacaoEventoNcGQuali.Enviando && e.EnviandoDesdeUtc < expirou)))
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Situacao, SituacaoEventoNcGQuali.Enviando)
                    .SetProperty(e => e.EnviandoDesdeUtc, agora), ct);
            if (reservado == 0) continue; // outra réplica pegou
            var evento = await db.EventosNcGQuali.SingleAsync(e => e.Id == id, ct);
            await EnviarAsync(evento, ct);
            await db.SaveChangesAsync(ct);
            enviados++;
        }
        return enviados;
    }

    private async Task EnviarAsync(EventoNcGQuali evento, CancellationToken ct)
    {
        var o = opcoes.Value;
        evento.Tentativas++;
        evento.EnviandoDesdeUtc = null;
        try
        {
            _credencial ??= new ClientSecretCredential(o.TenantId, o.ClientId, o.ClientSecret);
            var token = await _credencial.GetTokenAsync(new TokenRequestContext([o.Scope]), ct);
            var corpo = JsonNode.Parse(evento.Payload)!.AsObject();
            if (!string.IsNullOrWhiteSpace(o.LinkNcBase))
                corpo["linkOrigem"] = $"{o.LinkNcBase.TrimEnd('/')}/#/nao-conformidades/{evento.NaoConformidadeId}";
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{o.BaseUrl.TrimEnd('/')}/api/integracoes/ncs/eventos")
            {
                Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            using var resp = await http.CreateClient().SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var r = await resp.Content.ReadFromJsonAsync<RespostaGQuali>(cancellationToken: ct);
                evento.Situacao = SituacaoEventoNcGQuali.Enviado;
                evento.EnviadoEmUtc = DateTime.UtcNow;
                evento.RespostaGQuali = r?.Situacao;
                evento.UltimoErro = null;
                return;
            }
            var texto = await resp.Content.ReadAsStringAsync(ct);
            Registrar(evento, resp.StatusCode, texto);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Reagendar(evento, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    // 400 = evento inválido (não melhora repetindo). Demais códigos: tenta de novo.
    public static void Registrar(EventoNcGQuali evento, HttpStatusCode status, string corpo)
    {
        var erro = $"{(int)status} {status}: {corpo}";
        if (status == HttpStatusCode.BadRequest)
        {
            evento.Situacao = SituacaoEventoNcGQuali.Recusado;
            evento.UltimoErro = Cortar(erro);
            return;
        }
        Reagendar(evento, erro);
    }

    public static void Reagendar(EventoNcGQuali evento, string erro)
    {
        evento.Situacao = SituacaoEventoNcGQuali.Pendente;
        evento.UltimoErro = Cortar(erro);
        // 1, 2, 4, 8... minutos, no máximo 1 hora.
        var minutos = Math.Min(60, Math.Pow(2, Math.Max(0, evento.Tentativas - 1)));
        evento.ProximaTentativaEmUtc = DateTime.UtcNow.AddMinutes(minutos);
    }

    private static string Cortar(string s) => s.Length <= 1000 ? s : s[..1000];

    private record RespostaGQuali(Guid EventoId, string? Situacao, Guid? NcId, string? Motivo, bool Repetido);
}
