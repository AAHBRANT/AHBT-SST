using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Cameras;

// Registra o "último contato" do agente de câmeras (base do "Agente sem contato" na tela). Vale para qualquer chamada
// autenticada do agente: envio de quadro e consulta da configuração.
internal static class ContatoAgenteCamera
{
    // O contato só é regravado a cada 30 s (ou quando a versão muda): vários quadros por segundo da mesma
    // obra disputariam a mesma linha do agente à toa.
    public static readonly TimeSpan IntervaloGravacao = TimeSpan.FromSeconds(30);

    // Falha aqui (inclusive conflito de versão da linha) nunca derruba a operação principal.
    public static async Task RegistrarAsync(IAppDbContext db, AgenteCamera agente, string? versaoInformada, ILogger logger, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var versao = string.IsNullOrWhiteSpace(versaoInformada) ? null : versaoInformada.Trim();
        if (versao is { Length: > 30 }) versao = versao[..30];

        var versaoMudou = versao is not null && versao != agente.Versao;
        var contatoRecente = agente.UltimoContatoEmUtc is { } ultimo && agora - ultimo < IntervaloGravacao;
        if (contatoRecente && !versaoMudou) return;

        agente.UltimoContatoEmUtc = agora;
        if (versao is not null) agente.Versao = versao;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Contato do agente de câmeras {AgenteId} não registrado.", agente.Id);
            db.DescartarAlteracoesPendentes();
        }
    }
}
