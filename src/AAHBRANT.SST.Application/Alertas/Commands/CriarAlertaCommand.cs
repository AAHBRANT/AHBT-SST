using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Commands;

// Criação manual (decisão de escopo, não citação literal do §34): a geração automática de alerta a
// partir de vencimento de ASO/Treinamento/EPI/PT etc. dependeria de um job agendado (Worker) que
// ainda não existe no projeto — fora desta fatia. Este Command cobre o registro manual do alerta,
// que é o pré-requisito para o restante do fluxo (tratar/escalonar/resolver/ignorar) existir.
public record CriarAlertaCommand(
    TipoAlerta Tipo,
    SeveridadeAlerta Severidade,
    string Titulo,
    string? Descricao,
    string EntidadeOrigemTipo,
    Guid EntidadeOrigemId,
    Guid? TrabalhadorId,
    Guid? ObraId,
    Guid? DestinatarioUsuarioId,
    DateTime? DataLimiteTratamento) : IRequest<Guid>;

public class CriarAlertaCommandValidator : AbstractValidator<CriarAlertaCommand>
{
    public CriarAlertaCommandValidator()
    {
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EntidadeOrigemTipo).NotEmpty().MaximumLength(60);
    }
}

public class CriarAlertaCommandHandler : IRequestHandler<CriarAlertaCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IFilaNotificacaoTeams _filaNotificacaoTeams;
    private readonly IFilaCalendarioTeams _filaCalendarioTeams;

    public CriarAlertaCommandHandler(
        IAppDbContext db, IFilaNotificacaoTeams filaNotificacaoTeams, IFilaCalendarioTeams filaCalendarioTeams)
    {
        _db = db;
        _filaNotificacaoTeams = filaNotificacaoTeams;
        _filaCalendarioTeams = filaCalendarioTeams;
    }

    public async Task<Guid> Handle(CriarAlertaCommand request, CancellationToken ct)
    {
        if (request.TrabalhadorId.HasValue &&
            !await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId, ct))
            throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        if (request.ObraId.HasValue &&
            !await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (request.DestinatarioUsuarioId.HasValue &&
            !await _db.Usuarios.AnyAsync(u => u.Id == request.DestinatarioUsuarioId, ct))
            throw new KeyNotFoundException($"Usuário {request.DestinatarioUsuarioId} não encontrado.");

        var alerta = new Alerta
        {
            Tipo = request.Tipo,
            Severidade = request.Severidade,
            Titulo = request.Titulo,
            Descricao = request.Descricao,
            EntidadeOrigemTipo = request.EntidadeOrigemTipo,
            EntidadeOrigemId = request.EntidadeOrigemId,
            TrabalhadorId = request.TrabalhadorId,
            ObraId = request.ObraId,
            DestinatarioUsuarioId = request.DestinatarioUsuarioId,
            DataLimiteTratamento = request.DataLimiteTratamento,
        };

        _db.Alertas.Add(alerta);
        await _db.SaveChangesAsync(ct);

        // Envio proativo no Teams — enfileira e segue em frente (PROJECT RULES.md §4); o envio de
        // fato e o retry em caso de falha acontecem em background (ver IFilaNotificacaoTeams).
        if (alerta.DestinatarioUsuarioId.HasValue)
        {
            await _filaNotificacaoTeams.EnfileirarAsync(
                new NotificacaoTeamsMensagem(alerta.Id, alerta.DestinatarioUsuarioId.Value, alerta.Titulo, alerta.Descricao),
                ct);
        }

        // Canal de calendário do Teams — só enfileira com destinatário e data (docs/superpowers/specs/
        // 2026-08-28-calendario-teams-design.md §4.4).
        if (alerta.DestinatarioUsuarioId.HasValue && alerta.DataLimiteTratamento.HasValue)
        {
            await _filaCalendarioTeams.EnfileirarAsync(
                new CalendarioTeamsMensagem(
                    AlertaEngineService.OrigemCalendarioAlerta, alerta.Id, OperacaoCalendarioTeams.Criar,
                    alerta.DestinatarioUsuarioId.Value, alerta.Titulo, alerta.Descricao, alerta.DataLimiteTratamento.Value),
                ct);
        }

        return alerta.Id;
    }
}
