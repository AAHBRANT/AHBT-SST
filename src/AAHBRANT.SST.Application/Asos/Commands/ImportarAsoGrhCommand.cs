using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Asos.Commands;

// Carga (repetível) dos ASOs lidos direto do banco do G-RH — mesmo padrão de
// ImportarAlojamentosGrhCommand. Diferente de Colaborador/Alojamento, aqui não há "carga inicial
// única": como a leitura é o banco inteiro a cada vez (não um evento incremental), este mesmo
// comando é reusado tanto pelo botão manual quanto pelo polling periódico (GrhDbPollingService).
public record ImportarAsoGrhCommand : IRequest<ImportarAsoGrhResultado>;

// Desativados: ASOs que sumiram do G-RH e foram desativados nesta rodada. DesativacaoIgnorada: motivo, quando a
// limpeza foi barrada por segurança.
public record ImportarAsoGrhResultado(int TotalRecebidos, int TotalSincronizados, IReadOnlyList<string> Erros,
    int Desativados = 0, string? DesativacaoIgnorada = null);

public class ImportarAsoGrhCommandHandler : IRequestHandler<ImportarAsoGrhCommand, ImportarAsoGrhResultado>
{
    private readonly IAsoGrhClient _client;
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<ImportarAsoGrhCommandHandler> _logger;

    public ImportarAsoGrhCommandHandler(
        IAsoGrhClient client, IMediator mediator, IAppDbContext db, IEmailService email, ILogger<ImportarAsoGrhCommandHandler> logger)
    {
        _client = client;
        _mediator = mediator;
        _db = db;
        _email = email;
        _logger = logger;
    }

    public async Task<ImportarAsoGrhResultado> Handle(ImportarAsoGrhCommand request, CancellationToken ct)
    {
        var asos = await _client.ListarTodosAsync(ct);
        var erros = new List<string>();
        var totalSincronizados = 0;

        foreach (var a in asos)
        {
            try
            {
                await _mediator.Send(SincronizarAsoGrhCommand.DoAsoGrh(a), ct);
                totalSincronizados++;
            }
            catch (Exception ex)
            {
                erros.Add($"ASO (id G-RH {a.GrhAsoId}): {ex.Message}");
                // Mesmo cuidado de ImportarColaboradoresGrhCommand/ImportarAlojamentosGrhCommand:
                // descarta o rastreamento da entidade que falhou, senão derruba o SaveChanges de
                // todos os ASOs seguintes no lote.
                _db.DescartarAlteracoesPendentes();
            }
        }

        var (desativados, ignorada) = await DesativarQueSumiramDaOrigemAsync(asos, ct);

        // O aviso nunca derruba a sincronização: a desativação já foi gravada, e cada envio trata a própria falha.
        try
        {
            await AvisoAsoDesativadoGrh.EnviarAsync(_db, _email, _mediator, _logger, desativados, DateTime.UtcNow.Date, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Aviso de ASO desativado por sumir do G-RH falhou.");
        }

        return new ImportarAsoGrhResultado(asos.Count, totalSincronizados, erros, desativados.Count, ignorada);
    }

    // A leitura é o G-RH inteiro, então um ASO que veio de lá (GrhAsoId) e não está mais na lista foi
    // excluído, retipado ou perdeu a data de emissão na origem. Sem isto ele ficava parado no SST como
    // "Pendente" (caso Genyson, 10/2026) e as telas que escolhem um só ASO por pessoa bloqueavam quem já
    // tinha ASO apto válido. Desativa qualquer ASO que sumiu, com ou sem resultado: para o SST só importam a data
    // e se está apto, e a origem é a fonte da verdade dos ASOs que vieram dela. A limpeza só roda depois de a
    // leitura inteira ter dado certo (uma falha de leitura lança antes de chegar aqui) e tem três travas:
    //  - lista vazia: provável erro de configuração/limpeza do G-RH, não desativa nada;
    //  - volume: se sumiu gente demais de uma vez (mais de 25% dos ASOs ativos do G-RH, mínimo de 10),
    //    não desativa e avisa;
    //  - só ASO que veio do G-RH (nunca os lançados no SST).
    // Desativar é soft delete (Ativo = false): nada é apagado, o ASO continua guardado para fiscalização.
    private async Task<(IReadOnlyList<Aso> Desativados, string? Ignorada)> DesativarQueSumiramDaOrigemAsync(
        IReadOnlyList<AsoGrhDto> daOrigem, CancellationToken ct)
    {
        if (daOrigem.Count == 0)
            return (Array.Empty<Aso>(), "G-RH devolveu lista vazia de ASO; nenhuma desativação feita.");

        var idsNaOrigem = daOrigem.Select(a => a.GrhAsoId).ToHashSet();
        var ativosDoGrh = await _db.Asos.Where(a => a.GrhAsoId != null).ToListAsync(ct);
        var sumiram = ativosDoGrh.Where(a => !idsNaOrigem.Contains(a.GrhAsoId!)).ToList();
        if (sumiram.Count == 0)
            return (Array.Empty<Aso>(), null);

        var limite = Math.Max(10, ativosDoGrh.Count / 4);
        if (sumiram.Count > limite)
            return (Array.Empty<Aso>(), $"{sumiram.Count} ASOs sumiram do G-RH de uma vez (limite {limite}); nenhuma desativação feita, confira a origem.");

        foreach (var aso in sumiram)
            aso.Ativo = false;
        await _db.SaveChangesAsync(ct);

        return (sumiram, null);
    }
}
