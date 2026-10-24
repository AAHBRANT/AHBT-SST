using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;

namespace AAHBRANT.SST.Application.Asos.Commands;

// Carga (repetível) dos ASOs lidos direto do banco do G-RH — mesmo padrão de
// ImportarAlojamentosGrhCommand. Diferente de Colaborador/Alojamento, aqui não há "carga inicial
// única": como a leitura é o banco inteiro a cada vez (não um evento incremental), este mesmo
// comando é reusado tanto pelo botão manual quanto pelo polling periódico (GrhDbPollingService).
public record ImportarAsoGrhCommand : IRequest<ImportarAsoGrhResultado>;

public record ImportarAsoGrhResultado(int TotalRecebidos, int TotalSincronizados, IReadOnlyList<string> Erros);

public class ImportarAsoGrhCommandHandler : IRequestHandler<ImportarAsoGrhCommand, ImportarAsoGrhResultado>
{
    private readonly IAsoGrhClient _client;
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;

    public ImportarAsoGrhCommandHandler(IAsoGrhClient client, IMediator mediator, IAppDbContext db)
    {
        _client = client;
        _mediator = mediator;
        _db = db;
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

        return new ImportarAsoGrhResultado(asos.Count, totalSincronizados, erros);
    }
}
