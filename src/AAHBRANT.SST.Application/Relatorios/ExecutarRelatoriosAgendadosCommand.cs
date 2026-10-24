using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Relatorios;

// Chamado pelo Worker a cada poucos minutos. Às 08:00 (horário de Brasília) ou depois, uma vez por dia, dispara a
// lista de presença dos DDS ainda não informados. Se o Worker estiver fora do ar às 08:00, o disparo acontece
// assim que ele voltar, no mesmo dia. Devolve quantos relatórios foram publicados nesta chamada.
public record ExecutarRelatoriosAgendadosCommand(DateTime? AgoraBrasilia = null) : IRequest<int>;

public class ExecutarRelatoriosAgendadosCommandHandler : IRequestHandler<ExecutarRelatoriosAgendadosCommand, int>
{
    public static readonly TimeSpan HorarioDaListaDePresenca = new(8, 0, 0);

    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;

    public ExecutarRelatoriosAgendadosCommandHandler(IAppDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<int> Handle(ExecutarRelatoriosAgendadosCommand request, CancellationToken ct)
    {
        var agora = request.AgoraBrasilia ?? FusoBrasilia.Agora();
        if (agora.TimeOfDay < HorarioDaListaDePresenca) return 0;

        var hoje = agora.Date;
        if (await _db.ExecucoesRelatorioAgendado.AnyAsync(e => e.Tipo == TipoRelatorio.ListaPresencaDds && e.Data == hoje, ct))
            return 0;

        var publicados = await _mediator.Send(new GerarListasDePresencaCommand(), ct);

        _db.ExecucoesRelatorioAgendado.Add(new ExecucaoRelatorioAgendado
        {
            Tipo = TipoRelatorio.ListaPresencaDds,
            Data = hoje,
            ExecutadoEm = DateTime.UtcNow,
            RelatoriosGerados = publicados,
        });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Outra instância do Worker registrou o disparo de hoje primeiro: está tudo certo.
        }
        return publicados;
    }
}
