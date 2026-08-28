using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Commands;

public record ResolverAlertaCommand(Guid Id) : IRequest;

public class ResolverAlertaCommandValidator : AbstractValidator<ResolverAlertaCommand>
{
    public ResolverAlertaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ResolverAlertaCommandHandler : IRequestHandler<ResolverAlertaCommand>
{
    private readonly IAppDbContext _db;
    private readonly IFilaCalendarioTeams _filaCalendarioTeams;

    public ResolverAlertaCommandHandler(IAppDbContext db, IFilaCalendarioTeams filaCalendarioTeams)
    {
        _db = db;
        _filaCalendarioTeams = filaCalendarioTeams;
    }

    public async Task Handle(ResolverAlertaCommand request, CancellationToken ct)
    {
        var alerta = await _db.Alertas.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Alerta {request.Id} não encontrado.");

        if (alerta.Status is StatusAlerta.Resolvido or StatusAlerta.Ignorado)
            throw new InvalidOperationException("Este alerta já está encerrado.");

        alerta.Status = StatusAlerta.Resolvido;
        await _db.SaveChangesAsync(ct);

        // Canal de calendário do Teams — cancela só se havia um evento de fato criado (docs/superpowers/
        // specs/2026-08-28-calendario-teams-design.md §4.4).
        if (alerta.DestinatarioUsuarioId.HasValue)
        {
            var existeEventoCriado = await _db.CalendariosEventosTeams.AnyAsync(
                c => c.EntidadeOrigemTipo == AlertaEngineService.OrigemCalendarioAlerta
                    && c.EntidadeOrigemId == alerta.Id
                    && c.Status == StatusCalendarioEvento.Criado,
                ct);

            if (existeEventoCriado)
            {
                await _filaCalendarioTeams.EnfileirarAsync(
                    new CalendarioTeamsMensagem(
                        AlertaEngineService.OrigemCalendarioAlerta, alerta.Id, OperacaoCalendarioTeams.Cancelar,
                        alerta.DestinatarioUsuarioId.Value, null, null, null),
                    ct);
            }
        }
    }
}
