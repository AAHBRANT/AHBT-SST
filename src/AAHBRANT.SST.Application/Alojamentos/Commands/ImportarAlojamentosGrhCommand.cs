using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;

namespace AAHBRANT.SST.Application.Alojamentos.Commands;

// Carga inicial única do cadastro de alojamentos do G-RH (mesmo padrão de
// ImportarColaboradoresGrhCommand — GET /api/integracoes/sst/alojamentos, sem paginação nem filtro
// incremental, "roda uma vez, importa tudo"). A atualização contínua depois disso é por evento via
// Service Bus (ServiceBusAlojamentoGrhProcessor), não por repetir esta carga.
public record ImportarAlojamentosGrhCommand : IRequest<ImportarAlojamentosGrhResultado>;

public record ImportarAlojamentosGrhResultado(int TotalRecebidos, int TotalSincronizados, IReadOnlyList<string> Erros);

public class ImportarAlojamentosGrhCommandHandler : IRequestHandler<ImportarAlojamentosGrhCommand, ImportarAlojamentosGrhResultado>
{
    private readonly IAlojamentoGrhClient _client;
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;

    public ImportarAlojamentosGrhCommandHandler(IAlojamentoGrhClient client, IMediator mediator, IAppDbContext db)
    {
        _client = client;
        _mediator = mediator;
        _db = db;
    }

    public async Task<ImportarAlojamentosGrhResultado> Handle(ImportarAlojamentosGrhCommand request, CancellationToken ct)
    {
        var alojamentos = await _client.ListarTodosAsync(ct);
        var erros = new List<string>();
        var totalSincronizados = 0;

        foreach (var a in alojamentos)
        {
            try
            {
                await _mediator.Send(SincronizarAlojamentoGrhCommand.DoAlojamentoGrh(a), ct);
                totalSincronizados++;
            }
            catch (Exception ex)
            {
                erros.Add($"Alojamento '{a.Nome}' (id G-RH {a.GrhAlojamentoId}): {ex.Message}");
                // Mesmo cuidado de ImportarColaboradoresGrhCommand: descarta o rastreamento da entidade
                // que falhou, senão ela derruba o SaveChanges de todos os alojamentos seguintes no lote.
                _db.DescartarAlteracoesPendentes();
            }
        }

        return new ImportarAlojamentosGrhResultado(alojamentos.Count, totalSincronizados, erros);
    }
}
