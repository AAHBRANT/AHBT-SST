using AAHBRANT.SST.Application.Assinatura;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Atende os pedidos da FilaReparoGrupoFacial a qualquer hora do dia: confere o grupo da obra (modelo, grupo
// ausente/vazio e vínculos quebrados entre banco e Azure) e reconstrói das fotos guardadas só se precisar.
// Grupo íntegro custa duas leituras no Azure. A trava por obra (TravaSqlServer) impede duas réplicas de
// reconstruírem juntas — a corrida que quebrou os vínculos em 19/11.
public class ReparoGrupoFacialBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly FilaReparoGrupoFacial _fila;
    private readonly AssinaturaOptions _options;
    private readonly ILogger<ReparoGrupoFacialBackgroundService> _logger;

    public ReparoGrupoFacialBackgroundService(IServiceProvider services, FilaReparoGrupoFacial fila,
        IOptions<AssinaturaOptions> options, ILogger<ReparoGrupoFacialBackgroundService> logger)
    {
        _services = services;
        _fila = fila;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AzureFaceApiEndpoint) || string.IsNullOrWhiteSpace(_options.AzureFaceApiKey))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid obraId;
            try
            {
                obraId = await _fila.RetirarAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                using var escopo = _services.CreateScope();
                var facial = escopo.ServiceProvider.GetRequiredService<IAutenticacaoFacialService>();
                var resultado = await facial.MigrarModeloReconhecimentoAsync(obraId, stoppingToken);
                if (resultado is null)
                    continue; // grupo íntegro: a falha era rosto desconhecido ou foto ruim, não o grupo
                _logger.LogWarning(
                    "Guardião facial: grupo da obra {ObraId} estava quebrado e foi reconstruído automaticamente com {Migrados} cadastro(s).",
                    obraId, resultado.Migrados);
                if (resultado.SemCadastroAposMigracao.Count > 0)
                    _logger.LogWarning("Guardião facial: obra {ObraId} — cadastro facial precisa ser refeito para {Nomes}.",
                        obraId, string.Join(", ", resultado.SemCadastroAposMigracao));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Guardião facial: falha ao conferir/reconstruir o grupo da obra {ObraId}; a conferência noturna tenta de novo.", obraId);
            }
        }
    }
}
