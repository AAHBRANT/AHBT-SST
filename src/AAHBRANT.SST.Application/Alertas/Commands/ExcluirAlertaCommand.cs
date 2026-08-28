using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Commands;

public record ExcluirAlertaCommand(Guid Id) : IRequest;

public class ExcluirAlertaCommandValidator : AbstractValidator<ExcluirAlertaCommand>
{
    public ExcluirAlertaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirAlertaCommandHandler : IRequestHandler<ExcluirAlertaCommand>
{
    private readonly IAppDbContext _db;
    private readonly IFilaCalendarioTeams _filaCalendarioTeams;

    public ExcluirAlertaCommandHandler(IAppDbContext db, IFilaCalendarioTeams filaCalendarioTeams)
    {
        _db = db;
        _filaCalendarioTeams = filaCalendarioTeams;
    }

    public async Task Handle(ExcluirAlertaCommand request, CancellationToken ct)
    {
        var alerta = await _db.Alertas.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Alerta {request.Id} não encontrado.");

        var existeEventoCriado = alerta.DestinatarioUsuarioId.HasValue && await _db.CalendariosEventosTeams.AnyAsync(
            c => c.EntidadeOrigemTipo == AlertaEngineService.OrigemCalendarioAlerta
                && c.EntidadeOrigemId == alerta.Id
                && c.Status == StatusCalendarioEvento.Criado,
            ct);

        _db.Alertas.Remove(alerta);
        await _db.SaveChangesAsync(ct);

        // Canal de calendário do Teams — cancela só se havia um evento de fato criado (docs/superpowers/
        // specs/2026-08-28-calendario-teams-design.md §4.4).
        if (existeEventoCriado)
        {
            await _filaCalendarioTeams.EnfileirarAsync(
                new CalendarioTeamsMensagem(
                    AlertaEngineService.OrigemCalendarioAlerta, alerta.Id, OperacaoCalendarioTeams.Cancelar,
                    alerta.DestinatarioUsuarioId!.Value, null, null, null),
                ct);
        }
    }
}
