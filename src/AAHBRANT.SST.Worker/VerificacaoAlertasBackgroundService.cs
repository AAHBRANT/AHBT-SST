using AAHBRANT.SST.Application.Alertas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Worker;

// Job em ciclo (não cron — mais simples de operar como serviço long-running em container): a cada
// IntervaloExecucaoMinutos, gera alertas de vencimento (ASO/Treinamento/EPI/Documento de Gestão) e
// escalona os que passaram do prazo de tratamento sem resposta. A regra de negócio mora em
// VerificacaoAutomaticaAlertasService (Application) — este arquivo só cuida do agendamento,
// logging e de garantir que uma falha num ciclo não derruba o processo inteiro.
public class VerificacaoAlertasBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AlertasOptions _opcoes;
    private readonly ILogger<VerificacaoAlertasBackgroundService> _logger;

    public VerificacaoAlertasBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AlertasOptions> opcoes,
        ILogger<VerificacaoAlertasBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromMinutes(Math.Max(1, _opcoes.IntervaloExecucaoMinutos));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecutarCicloAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falha ao executar o ciclo de verificação de alertas.");
            }

            try
            {
                await Task.Delay(intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ExecutarCicloAsync(CancellationToken ct)
    {
        // VerificacaoAutomaticaAlertasService é Scoped (depende de IAppDbContext) — precisa de um
        // escopo próprio por ciclo, já que este BackgroundService em si é Singleton.
        using var escopo = _scopeFactory.CreateScope();
        var servico = escopo.ServiceProvider.GetRequiredService<VerificacaoAutomaticaAlertasService>();

        var vencimentosCriados = await servico.VerificarVencimentosAsync(_opcoes.DiasAntecedenciaVencimento, ct);
        var escalonados = await servico.EscalonarPendentesAsync(ct);

        _logger.LogInformation(
            "Ciclo de alertas concluído: {VencimentosCriados} alerta(s) de vencimento criado(s), {Escalonados} alerta(s) escalonado(s).",
            vencimentosCriados, escalonados);
    }
}
