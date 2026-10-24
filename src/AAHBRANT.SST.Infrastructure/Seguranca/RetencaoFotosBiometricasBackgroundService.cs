using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

// Prazo de guarda das fotos de evidência das assinaturas (rosto/digital), LGPD art. 15/16. DESLIGADO por
// padrão: o prazo é decisão do jurídico e do encarregado de dados (DPO), porque a foto também é prova da
// assinatura. Para ligar, configure Lgpd:DiasRetencaoFotosEvidencia (ex.: 1825 = 5 anos). Só apaga a imagem
// de documento já finalizado; o hash SHA-256 da foto continua gravado, provando qual imagem existia.
// Roda uma vez por dia.
public class RetencaoFotosBiometricasBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RetencaoFotosBiometricasBackgroundService> _logger;
    private readonly int _dias;

    public RetencaoFotosBiometricasBackgroundService(IServiceProvider services, ILogger<RetencaoFotosBiometricasBackgroundService> logger, IConfiguration configuration)
    {
        _services = services;
        _logger = logger;
        _dias = configuration.GetValue<int?>("Lgpd:DiasRetencaoFotosEvidencia") ?? 0;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_dias <= 0)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var escopo = _services.CreateScope();
                var db = escopo.ServiceProvider.GetRequiredService<SstDbContext>();
                var limite = DateTime.UtcNow.AddDays(-_dias);
                var vencidas = await db.DocumentoSignatarios.IgnoreQueryFilters()
                    .Where(s => s.FotoEvidenciaConteudo != null && s.AssinadoEm < limite
                        && s.DocumentoAssinatura!.Status == StatusDocumentoAssinatura.Finalizado)
                    .Take(500)
                    .ToListAsync(stoppingToken);
                foreach (var s in vencidas)
                    s.FotoEvidenciaConteudo = null;
                if (vencidas.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Prazo de guarda: {Quantidade} foto(s) de evidência apagadas (hash mantido).", vencidas.Count);
                    continue; // pode haver mais: processa o próximo lote já
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao aplicar o prazo de guarda das fotos de evidência.");
            }

            try { await Task.Delay(TimeSpan.FromDays(1), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
