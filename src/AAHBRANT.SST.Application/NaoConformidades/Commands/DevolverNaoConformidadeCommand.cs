using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades.Commands;

// Procedimento de Inspeção Técnica de Campo (§6.7/§10) — botão DEVOLVER: o inspetor, ao validar a
// conclusão, pode devolver a ocorrência ao responsável quando a execução não atende ao esperado.
// Reabre a AcaoPlano mais recente vinculada (a mesma que RegistrarConclusaoNaoConformidadeCommand
// concluiu) de volta para EmAndamento — limpando DataConclusao — e move a NC para Devolvida, de onde
// ResponderNaoConformidadeCommand a leva de volta a EmAndamento após nova resposta do responsável.
// Notifica o responsável de forma imediata, mesmo padrão de Alerta real + fila Teams usado em
// EnviarNaoConformidadeCommand (NotificacaoTeamsMensagem.AlertaId tem FK real para Alertas).
public record DevolverNaoConformidadeCommand(Guid Id, string Motivo) : IRequest;

public class DevolverNaoConformidadeCommandValidator : AbstractValidator<DevolverNaoConformidadeCommand>
{
    public DevolverNaoConformidadeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(1000);
    }
}

public class DevolverNaoConformidadeCommandHandler : IRequestHandler<DevolverNaoConformidadeCommand>
{
    private readonly IAppDbContext _db;
    private readonly IFilaNotificacaoTeams _filaNotificacaoTeams;

    public DevolverNaoConformidadeCommandHandler(IAppDbContext db, IFilaNotificacaoTeams filaNotificacaoTeams)
    {
        _db = db;
        _filaNotificacaoTeams = filaNotificacaoTeams;
    }

    public async Task Handle(DevolverNaoConformidadeCommand request, CancellationToken ct)
    {
        var nc = await _db.NaoConformidades
            .Include(n => n.Atividade)
            .FirstOrDefaultAsync(n => n.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Não conformidade {request.Id} não encontrada.");

        if (nc.Status != StatusNaoConformidade.AguardandoValidacao)
            throw new InvalidOperationException(
                "Só é possível devolver uma ocorrência que esteja Aguardando validação.");

        if (!nc.ResponsavelUsuarioId.HasValue)
            throw new InvalidOperationException("Ocorrência sem responsável definido para devolver.");

        var acao = await _db.AcoesPlano
            .Where(a => a.OrigemTipo == nameof(NaoConformidade) && a.OrigemId == nc.Id)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "Não há ação de plano vinculada a esta ocorrência para reabrir.");

        acao.Status = StatusControleRisco.EmAndamento;
        acao.DataConclusao = null;

        nc.MotivoDevolucao = request.Motivo;
        nc.Status = StatusNaoConformidade.Devolvida;

        var alerta = new Alerta
        {
            Tipo = TipoAlerta.NaoConformidadeAberta,
            Severidade = SeveridadeAlerta.Atencao,
            Titulo = "Ocorrência de inspeção devolvida para nova tratativa",
            Descricao = request.Motivo,
            EntidadeOrigemTipo = nameof(NaoConformidade),
            EntidadeOrigemId = nc.Id,
            ObraId = nc.Atividade?.ObraId,
            DestinatarioUsuarioId = nc.ResponsavelUsuarioId,
        };
        _db.Alertas.Add(alerta);

        await _db.SaveChangesAsync(ct);

        await _filaNotificacaoTeams.EnfileirarAsync(
            new NotificacaoTeamsMensagem(alerta.Id, nc.ResponsavelUsuarioId.Value, alerta.Titulo, alerta.Descricao),
            ct);
    }
}
