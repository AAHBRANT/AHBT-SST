using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Leva os grupos de reconhecimento facial das obras para o recognition_04 (ver
// AzureFaceAutenticacaoStrategy.MigrarModeloReconhecimentoAsync), uma obra por vez e só na janela da
// madrugada, porque usa a mesma cota do Azure das assinaturas. Quando uma passada inteira não encontra
// mais nada para migrar, o serviço termina — não fica consultando o Azure para sempre.
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DentroDaJanela(DateTime.UtcNow) && !await MigrarObrasAsync(stoppingToken))
                {
                    _logger.LogInformation("Migração do reconhecimento facial para {Modelo} concluída em todas as obras.", AzureFaceAutenticacaoStrategy.ModeloReconhecimentoAtual);
                    return;
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
                _logger.LogInformation("Obra {ObraId}: {Migrados} cadastro(s) facial(is) migrados para {Modelo}.",
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

    // Janela em horário de Brasília (UTC-3, sem horário de verão desde 2019). Ex.: 22h às 5h.
    private bool DentroDaJanela(DateTime agoraUtc)
    {
        var hora = agoraUtc.AddHours(-3).Hour;
        var inicio = _options.MigracaoFacialHoraInicio;
        var fim = _options.MigracaoFacialHoraFim;
        return inicio <= fim ? hora >= inicio && hora < fim : hora >= inicio || hora < fim;
    }
}
