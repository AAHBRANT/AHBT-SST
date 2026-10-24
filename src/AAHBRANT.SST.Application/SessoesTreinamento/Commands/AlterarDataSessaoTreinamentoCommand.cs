using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.PlanosIntegracao;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Ajuste da data da turma (especificação de 21/10/2026, etapa 3): a turma agendada automaticamente
// para o funcionário novo nasce com data sugerida, e o técnico confirma ou muda pelo pop-up. Só antes
// de qualquer presença registrada — depois disso a data é a do treinamento que de fato aconteceu.
public record AlterarDataSessaoTreinamentoCommand(Guid Id, DateTime DataRealizacao) : IRequest;

public class AlterarDataSessaoTreinamentoCommandValidator : AbstractValidator<AlterarDataSessaoTreinamentoCommand>
{
    public AlterarDataSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DataRealizacao).NotEmpty();
    }
}

public class AlterarDataSessaoTreinamentoCommandHandler : IRequestHandler<AlterarDataSessaoTreinamentoCommand>
{
    private readonly IAppDbContext _db;
    private readonly IFilaCalendarioTeams _filaCalendario;

    public AlterarDataSessaoTreinamentoCommandHandler(IAppDbContext db, IFilaCalendarioTeams filaCalendario)
    {
        _db = db;
        _filaCalendario = filaCalendario;
    }

    public async Task Handle(AlterarDataSessaoTreinamentoCommand request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Turma de treinamento não encontrada.");
        if (sessao.Status != StatusSessaoTreinamento.EmAndamento)
            throw new InvalidOperationException("Esta turma já foi encerrada.");
        if (request.DataRealizacao.Date < AgendaTreinamentosIntegracao.HojeBrasilia())
            throw new InvalidOperationException("Escolha hoje ou uma data futura.");

        var participantes = await _db.ParticipantesSessaoTreinamento
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.Ativo)
            .Select(p => new { p.TrabalhadorId, p.PresencaConfirmadaEm })
            .ToListAsync(ct);
        if (participantes.Any(p => p.PresencaConfirmadaEm is not null))
            throw new InvalidOperationException("A turma já tem presença registrada; a data não pode mais mudar.");

        sessao.DataRealizacao = request.DataRealizacao.Date;
        await _db.SaveChangesAsync(ct);

        // Os eventos de calendário dos avisos de funcionário novo ficam na data da primeira turma de
        // cada um — recalcula para quem está nesta turma.
        var trabalhadores = participantes.Select(p => p.TrabalhadorId).ToList();
        var alertas = await _db.Alertas
            .Where(a => a.Tipo == TipoAlerta.IntegracaoFuncionarioNovo && a.Status == StatusAlerta.Aberto
                && a.TrabalhadorId != null && trabalhadores.Contains(a.TrabalhadorId.Value) && a.DestinatarioUsuarioId != null)
            .ToListAsync(ct);
        foreach (var grupo in alertas.GroupBy(a => a.TrabalhadorId!.Value))
        {
            var primeiraTurma = await _db.ParticipantesSessaoTreinamento
                .Where(p => p.TrabalhadorId == grupo.Key && p.Ativo && p.SessaoTreinamento!.Status == StatusSessaoTreinamento.EmAndamento)
                .MinAsync(p => (DateTime?)p.SessaoTreinamento!.DataRealizacao, ct);
            foreach (var alerta in grupo)
                await _filaCalendario.EnfileirarAsync(new CalendarioTeamsMensagem(
                    AlertaEngineService.OrigemCalendarioAlerta, alerta.Id, OperacaoCalendarioTeams.Atualizar,
                    alerta.DestinatarioUsuarioId!.Value, alerta.Titulo, alerta.Descricao, primeiraTurma ?? sessao.DataRealizacao), ct);
        }
    }
}
