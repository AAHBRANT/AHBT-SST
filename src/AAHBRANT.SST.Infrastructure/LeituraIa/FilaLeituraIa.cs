using System.Threading.Channels;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Infrastructure.LeituraIa;

// Fila em memória das leituras com IA (singleton). Quem consome é o LeituraIaBackgroundService, que só
// roda na Api (o Worker registra a fila pelo AddInfrastructure, mas não tem quem enfileire nele).
public class FilaLeituraIa : IFilaLeituraIa
{
    private readonly Channel<Guid> _canal = Channel.CreateUnbounded<Guid>();

    public ChannelReader<Guid> Leitor => _canal.Reader;

    public ValueTask EnfileirarAsync(Guid leituraId, CancellationToken ct = default) => _canal.Writer.WriteAsync(leituraId, ct);
}

public class LeituraIaBackgroundService : BackgroundService
{
    private readonly FilaLeituraIa _fila;
    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<LeituraIaBackgroundService> _logger;

    public LeituraIaBackgroundService(FilaLeituraIa fila, IServiceScopeFactory escopos, ILogger<LeituraIaBackgroundService> logger)
    {
        _fila = fila;
        _escopos = escopos;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RetomarInterrompidasAsync(stoppingToken);

        await foreach (var leituraId in _fila.Leitor.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var escopo = _escopos.CreateScope();
                await escopo.ServiceProvider.GetRequiredService<ProcessadorLeituraIa>().ProcessarAsync(leituraId, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Erro inesperado ao processar a leitura com IA {LeituraId}", leituraId);
            }
        }
    }

    // A fila é em memória: leitura que estava na fila ou no meio quando a Api reiniciou (deploy) volta
    // para o começo, em vez de ficar "lendo" para sempre.
    private async Task RetomarInterrompidasAsync(CancellationToken ct)
    {
        try
        {
            using var escopo = _escopos.CreateScope();
            var db = escopo.ServiceProvider.GetRequiredService<IAppDbContext>();
            var interrompidas = await db.LeiturasDocumentoIa
                .Where(l => l.Status == StatusLeituraIa.Pendente || l.Status == StatusLeituraIa.Lendo)
                .ToListAsync(ct);
            foreach (var l in interrompidas)
            {
                l.Status = StatusLeituraIa.Pendente;
                l.Etapa = "Retomada após reinício do sistema";
                l.PassosConcluidos = 0;
            }
            await db.SaveChangesAsync(ct);
            foreach (var l in interrompidas)
                await _fila.EnfileirarAsync(l.Id, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Não foi possível retomar leituras com IA interrompidas");
        }
    }
}
