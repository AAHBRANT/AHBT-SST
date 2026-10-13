using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

// Cifra as fotos biométricas gravadas antes de 12/10, quando ficavam em texto puro (ver
// FotoBiometricaCriptografiaConversor). Acha pelo SQL as linhas sem o prefixo de cifra e regrava cada uma
// pelo EF, que aplica o conversor. Roda ao iniciar a API, em lotes pequenos com pausa, e termina quando não
// sobra nada. Só em SQL Server (a busca pelo prefixo é SQL direto); nos testes, com banco em memória, não roda.
public class CriptografiaFotosBackgroundService : BackgroundService
{
    private const int TamanhoLote = 50;
    private static readonly TimeSpan PausaEntreLotes = TimeSpan.FromSeconds(2);
    // "SSTFOTO1" em hexadecimal: o prefixo que o conversor coloca na frente de toda foto cifrada.
    private const string PrefixoHex = "0x535354464F544F31";

    private readonly IServiceProvider _services;
    private readonly ILogger<CriptografiaFotosBackgroundService> _logger;

    public CriptografiaFotosBackgroundService(IServiceProvider services, ILogger<CriptografiaFotosBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Dá tempo de a API subir e aplicar as migrations antes de mexer no banco.
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            var total = 0;
            total += await CifrarAsync("DocumentoSignatarios", "FotoEvidenciaConteudo", (db, ids) => db.DocumentoSignatarios.IgnoreQueryFilters().Where(e => ids.Contains(e.Id)), e => e.FotoEvidenciaConteudo, stoppingToken);
            total += await CifrarAsync("FotosCadastroFacial", "Conteudo", (db, ids) => db.FotosCadastroFacial.IgnoreQueryFilters().Where(e => ids.Contains(e.Id)), e => e.Conteudo, stoppingToken);
            total += await CifrarAsync("DdsParticipantes", "FotoConteudo", (db, ids) => db.DdsParticipantes.IgnoreQueryFilters().Where(e => ids.Contains(e.Id)), e => e.FotoConteudo, stoppingToken);
            total += await CifrarAsync("Trabalhadores", "FotoConteudo", (db, ids) => db.Trabalhadores.IgnoreQueryFilters().Where(e => ids.Contains(e.Id)), e => e.FotoConteudo, stoppingToken);
            if (total > 0)
                _logger.LogInformation("Criptografia das fotos biométricas antigas concluída: {Total} registro(s).", total);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criptografar as fotos biométricas antigas; tenta de novo no próximo início da API.");
        }
    }

    private async Task<int> CifrarAsync<T>(string tabela, string coluna, Func<SstDbContext, List<Guid>, IQueryable<T>> carregar,
        System.Linq.Expressions.Expression<Func<T, byte[]?>> propriedade, CancellationToken ct) where T : class
    {
        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            using var escopo = _services.CreateScope();
            var db = escopo.ServiceProvider.GetRequiredService<SstDbContext>();
            if (!db.Database.IsSqlServer())
                return 0;

            // Nome de tabela/coluna vem de constantes deste arquivo, nunca de entrada externa.
#pragma warning disable EF1002
            var ids = await db.Database.SqlQueryRaw<Guid>(
                $"SELECT TOP ({TamanhoLote}) Id AS Value FROM [{tabela}] WHERE [{coluna}] IS NOT NULL AND DATALENGTH([{coluna}]) > 0 " +
                $"AND (DATALENGTH([{coluna}]) < 8 OR SUBSTRING([{coluna}], 1, 8) <> {PrefixoHex})").ToListAsync(ct);
#pragma warning restore EF1002
            if (ids.Count == 0)
                return total;

            foreach (var entidade in await carregar(db, ids).ToListAsync(ct))
                db.Entry(entidade).Property(propriedade).IsModified = true;
            await db.SaveChangesAsync(ct);
            total += ids.Count;
            await Task.Delay(PausaEntreLotes, ct);
        }
        return total;
    }
}
