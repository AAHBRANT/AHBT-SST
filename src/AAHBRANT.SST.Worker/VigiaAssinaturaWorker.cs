using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Worker;

// Vigia da assinatura (pedido do usuário, 27/11): a cada 10 minutos testa o reconhecimento facial com uma
// consulta leve ao Azure. Se cair, avisa no Telegram do responsável e chama o agente engenheiro do Claude
// Console para investigar — sem esperar a obra descobrir no DDS. Sem IA aqui: código simples e barato.
// Liga com AgenteAssinatura:VigiaHabilitada=true.
public class VigiaAssinaturaWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<VigiaAssinaturaWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly VigiaAssinatura _vigia = new();

    public VigiaAssinaturaWorker(IServiceProvider services, ILogger<VigiaAssinaturaWorker> logger, IConfiguration configuration)
    {
        _services = services;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>("AgenteAssinatura:VigiaHabilitada"))
        {
            _logger.LogInformation("Vigia da assinatura desligada (AgenteAssinatura:VigiaHabilitada).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await VerificarAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha na vigia da assinatura.");
            }

            try
            {
                await Task.Delay(VigiaAssinatura.Intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task VerificarAsync(CancellationToken ct)
    {
        using var escopo = _services.CreateScope();
        var facial = escopo.ServiceProvider.GetRequiredService<IAutenticacaoFacialService>();

        var resultado = await facial.VerificarServicoAsync(ct);
        if (!resultado.Ok)
        {
            await Task.Delay(VigiaAssinatura.EsperaConfirmacao, ct);
            resultado = await facial.VerificarServicoAsync(ct);
        }

        var agora = DateTime.UtcNow;
        var decisao = _vigia.Avaliar(resultado, agora);
        if (decisao == DecisaoVigiaAssinatura.Nada)
            return;

        var agenteChamado = false;
        if (decisao == DecisaoVigiaAssinatura.AvisarQueda)
        {
            _logger.LogWarning("Reconhecimento facial fora: {Endpoint} {Status} {Codigo} {Mensagem}",
                resultado.Endpoint, resultado.StatusHttp, resultado.CodigoErro, resultado.Mensagem);
            var disparador = escopo.ServiceProvider.GetRequiredService<IAgenteAssinaturaDisparador>();
            if (disparador.Configurado)
            {
                try
                {
                    var sessao = await disparador.DispararAsync(ct);
                    agenteChamado = sessao is not null;
                    _logger.LogInformation("Agente da assinatura chamado: sessão {Sessao}.", sessao);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Não foi possível chamar o agente da assinatura.");
                }
            }
        }
        else if (decisao == DecisaoVigiaAssinatura.AvisarVolta)
        {
            _logger.LogInformation("Reconhecimento facial voltou a responder.");
        }

        var telegram = escopo.ServiceProvider.GetRequiredService<ITelegramAlertaService>();
        await telegram.EnviarAsync(_vigia.Mensagem(decisao, resultado, agora, agenteChamado), ct);

        // Queda e lembrete (no máximo um a cada 2 h) também ligam para o celular: de madrugada a mensagem
        // passa despercebida. A volta não liga.
        if (VigiaAssinatura.DeveLigar(decisao))
        {
            var ligacao = escopo.ServiceProvider.GetRequiredService<ILigacaoAlertaService>();
            if (ligacao.Configurada)
            {
                try
                {
                    var chamada = await ligacao.LigarAsync(_vigia.TextoLigacao(decisao, agora), ct);
                    _logger.LogInformation("Ligação de alerta da assinatura: {Chamada}.", chamada ?? "não feita");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Não foi possível ligar para o responsável.");
                }
            }
        }
    }
}
