using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Commands;

// Edita os dados descritivos do alerta. Não altera Status — isso é responsabilidade exclusiva dos
// Commands de transição (IniciarTratamento/Escalonar/Resolver/Ignorar), mesmo princípio já usado em
// PermissaoTrabalho e NaoConformidade.
public record AtualizarAlertaCommand(
    Guid Id,
    TipoAlerta Tipo,
    SeveridadeAlerta Severidade,
    string Titulo,
    string? Descricao,
    Guid? TrabalhadorId,
    Guid? ObraId,
    Guid? DestinatarioUsuarioId,
    DateTime? DataLimiteTratamento) : IRequest;

public class AtualizarAlertaCommandValidator : AbstractValidator<AtualizarAlertaCommand>
{
    public AtualizarAlertaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(200);
    }
}

public class AtualizarAlertaCommandHandler : IRequestHandler<AtualizarAlertaCommand>
{
    private readonly IAppDbContext _db;
    private readonly IFilaCalendarioTeams _filaCalendarioTeams;

    public AtualizarAlertaCommandHandler(IAppDbContext db, IFilaCalendarioTeams filaCalendarioTeams)
    {
        _db = db;
        _filaCalendarioTeams = filaCalendarioTeams;
    }

    public async Task Handle(AtualizarAlertaCommand request, CancellationToken ct)
    {
        var alerta = await _db.Alertas.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Alerta {request.Id} não encontrado.");

        if (request.TrabalhadorId.HasValue &&
            !await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId, ct))
            throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        if (request.ObraId.HasValue &&
            !await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (request.DestinatarioUsuarioId.HasValue &&
            !await _db.Usuarios.AnyAsync(u => u.Id == request.DestinatarioUsuarioId, ct))
            throw new KeyNotFoundException($"Usuário {request.DestinatarioUsuarioId} não encontrado.");

        alerta.Tipo = request.Tipo;
        alerta.Severidade = request.Severidade;
        alerta.Titulo = request.Titulo;
        alerta.Descricao = request.Descricao;
        alerta.TrabalhadorId = request.TrabalhadorId;
        alerta.ObraId = request.ObraId;
        alerta.DestinatarioUsuarioId = request.DestinatarioUsuarioId;
        alerta.DataLimiteTratamento = request.DataLimiteTratamento;

        await _db.SaveChangesAsync(ct);

        // Canal de calendário do Teams — Atualizar se já existe evento criado para este alerta,
        // senão Criar (docs/superpowers/specs/2026-08-28-calendario-teams-design.md §4.4).
        if (alerta.DestinatarioUsuarioId.HasValue && alerta.DataLimiteTratamento.HasValue)
        {
            var existeEventoCriado = await _db.CalendariosEventosTeams.AnyAsync(
                c => c.EntidadeOrigemTipo == AlertaEngineService.OrigemCalendarioAlerta
                    && c.EntidadeOrigemId == alerta.Id
                    && c.Status == StatusCalendarioEvento.Criado,
                ct);

            await _filaCalendarioTeams.EnfileirarAsync(
                new CalendarioTeamsMensagem(
                    AlertaEngineService.OrigemCalendarioAlerta, alerta.Id,
                    existeEventoCriado ? OperacaoCalendarioTeams.Atualizar : OperacaoCalendarioTeams.Criar,
                    alerta.DestinatarioUsuarioId.Value, alerta.Titulo, alerta.Descricao, alerta.DataLimiteTratamento.Value),
                ct);
        }
    }
}
