using AAHBRANT.SST.AgenteBiometria.Opcoes;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.AgenteBiometria.Servicos;

// Registra a chave pública do agente no servidor ao iniciar, tentando de novo a cada minuto enquanto não
// conseguir (sem rede, servidor fora). Sem isso o servidor recusa as leituras assinadas deste leitor.
public class RegistroChavePublicaService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    private readonly BackendClient _backend;
    private readonly ChaveAssinaturaAgente _chave;
    private readonly AgenteOptions _options;
    private readonly ILogger<RegistroChavePublicaService> _logger;

    public RegistroChavePublicaService(BackendClient backend, ChaveAssinaturaAgente chave, IOptions<AgenteOptions> options, ILogger<RegistroChavePublicaService> logger)
    {
        _backend = backend;
        _chave = chave;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.DispositivoId == Guid.Empty || string.IsNullOrWhiteSpace(_options.BackendBaseUrl))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var erro = await _backend.RegistrarChavePublicaAsync(_chave.ChavePublicaBase64, stoppingToken);
                if (erro is null)
                {
                    _logger.LogInformation("Chave de assinatura registrada no servidor.");
                    return;
                }
                _logger.LogError("O servidor recusou a chave de assinatura deste agente: {Erro}", erro);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Falha ao registrar a chave de assinatura; tentando de novo em 1 minuto.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
