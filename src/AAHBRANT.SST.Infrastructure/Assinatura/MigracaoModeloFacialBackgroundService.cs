using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Confere o grupo de reconhecimento facial de cada obra (ver AzureFaceAutenticacaoStrategy.
// MigrarModeloReconhecimentoAsync): modelo antigo, grupo ausente/vazio ou vínculos quebrados entre banco e
// Azure são reconstruídos das fotos guardadas. Uma obra por vez e só na janela da madrugada, porque usa a
// mesma cota do Azure das assinaturas.
//
// Guardião (23/11): antes, quando uma passada inteira não encontrava nada, o serviço TERMINAVA e nunca mais
// conferia — por isso a quebra de 19/11 na Beira Rio ficou escondida até a obra falhar de manhã (2ª vez).
// Agora faz uma passada completa por noite, sempre. Grupo íntegro custa duas leituras no Azure.
public class MigracaoModeloFacialBackgroundService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _services;
    private readonly AssinaturaOptions _options;
    private readonly ILogger<MigracaoModeloFacialBackgroundService> _logger;

    public MigracaoModeloFacialBackgroundService(IServiceProvider services, IOptions<AssinaturaOptions> options, ILogger<MigracaoModeloFacialBackgroundService> logger)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.MigrarModeloFacialAutomaticamente
            || string.IsNullOrWhiteSpace(_options.AzureFaceApiEndpoint) || string.IsNullOrWhiteSpace(_options.AzureFaceApiKey))
            return;

        DateTime? ultimaPassadaCompletaUtc = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var agora = DateTime.UtcNow;
                if (DeveConferir(agora, ultimaPassadaCompletaUtc, _options.MigracaoFacialHoraInicio, _options.MigracaoFacialHoraFim)
                    && !await MigrarObrasAsync(stoppingToken))
                {
                    // Passada inteira sem pendência nem falha: a próxima é só na noite seguinte. Se faltou algo
                    // (falha ou janela fechou no meio), tenta de novo em 15 minutos, ainda nesta noite.
                    ultimaPassadaCompletaUtc = agora;
                    _logger.LogInformation("Grupos faciais conferidos em todas as obras ({Modelo}).", AzureFaceAutenticacaoStrategy.ModeloReconhecimentoAtual);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha na migração do reconhecimento facial; tenta de novo na próxima janela.");
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // Devolve true enquanto ainda houver obra para migrar (ou se a janela fechou no meio).
    private async Task<bool> MigrarObrasAsync(CancellationToken ct)
    {
        List<Guid> obras;
        using (var escopo = _services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<IAppDbContext>();
            obras = await db.Obras.IgnoreQueryFilters()
                .Where(o => o.AzureFacePersonGroupId != null)
                .Select(o => o.Id)
                .ToListAsync(ct);
        }

        var falhou = false;
        foreach (var obraId in obras)
        {
            if (!DentroDaJanela(DateTime.UtcNow))
                return true;
            try
            {
                // Escopo novo por obra: DbContext limpo e sem acumular entidades das obras anteriores.
                using var escopo = _services.CreateScope();
                var facial = escopo.ServiceProvider.GetRequiredService<IAutenticacaoFacialService>();
                var resultado = await facial.MigrarModeloReconhecimentoAsync(obraId, ct);
                if (resultado is null)
                    continue;
                _logger.LogWarning("Obra {ObraId}: grupo facial reconstruído com {Migrados} cadastro(s) ({Modelo}).",
                    obraId, resultado.Migrados, AzureFaceAutenticacaoStrategy.ModeloReconhecimentoAtual);
                if (resultado.SemCadastroAposMigracao.Count > 0)
                    _logger.LogWarning("Obra {ObraId}: cadastro facial precisa ser refeito para {Nomes}.",
                        obraId, string.Join(", ", resultado.SemCadastroAposMigracao));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhou = true;
                _logger.LogError(ex, "Falha ao migrar o reconhecimento facial da obra {ObraId}.", obraId);
            }
        }
        return falhou;
    }

    private bool DentroDaJanela(DateTime agoraUtc)
        => DentroDaJanela(agoraUtc, _options.MigracaoFacialHoraInicio, _options.MigracaoFacialHoraFim);

    // Janela em horário de Brasília (UTC-3, sem horário de verão desde 2019). Ex.: 22h às 5h.
    private static bool DentroDaJanela(DateTime agoraUtc, int inicio, int fim)
    {
        var hora = agoraUtc.AddHours(-3).Hour;
        return inicio <= fim ? hora >= inicio && hora < fim : hora >= inicio || hora < fim;
    }

    // Dentro da janela e ainda sem passada completa nesta noite (desde o começo da janela atual).
    public static bool DeveConferir(DateTime agoraUtc, DateTime? ultimaPassadaCompletaUtc, int inicio, int fim)
    {
        if (!DentroDaJanela(agoraUtc, inicio, fim))
            return false;
        if (ultimaPassadaCompletaUtc is null)
            return true;

        var local = agoraUtc.AddHours(-3);
        var inicioLocal = local.Date.AddHours(inicio);
        if (inicio > fim && local.Hour < fim)
            inicioLocal = inicioLocal.AddDays(-1); // janela que vira a meia-noite: começou ontem
        var inicioJanelaUtc = inicioLocal.AddHours(3);
        return ultimaPassadaCompletaUtc.Value < inicioJanelaUtc;
    }
}
