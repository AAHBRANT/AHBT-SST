using AAHBRANT.SST.Application.Cameras.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Worker;

// Câmera IP "quase ao vivo": encerra, e apaga o último quadro, das sessões que pararam (tempo máximo, 60 s sem espectador, agente
// que não começou, câmera desligada, agente revogado) e que ninguém mais tocou. As rotas da API já encerram as paradas quando
// são chamadas; isto garante que a imagem de uma sessão abandonada não fique guardada no banco.
public class SessoesAoVivoCameraWorker : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _services;
    private readonly ILogger<SessoesAoVivoCameraWorker> _logger;

    public SessoesAoVivoCameraWorker(IServiceProvider services, ILogger<SessoesAoVivoCameraWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var escopo = _services.CreateScope();
                var encerradas = await escopo.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new LimparSessoesAoVivoCameraParadasCommand(), stoppingToken);
                if (encerradas > 0)
                    _logger.LogInformation("Câmera ao vivo: {Quantidade} sessão(ões) parada(s) encerrada(s).", encerradas);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao encerrar as sessões paradas da câmera ao vivo.");
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
