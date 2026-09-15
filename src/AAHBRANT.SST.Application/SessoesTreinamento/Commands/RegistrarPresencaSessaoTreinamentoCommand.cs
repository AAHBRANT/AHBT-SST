using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Presença por biometria (item 3 da proposta) — mesmo mecanismo já usado no DDS
// (RegistrarParticipanteCommand): reaproveita IAutenticacaoBiometriaLocalService, o match 1:N já
// aconteceu no agente local (leitor Futronic FS80H). Diferente do DDS, o participante já existe
// (pré-selecionado na criação da turma) — aqui só se confirma a presença dele, não se cria a linha.
public record RegistrarPresencaSessaoTreinamentoCommand(
    Guid SessaoTreinamentoId,
    Guid TrabalhadorId,
    Guid DispositivoId,
    string SegredoDispositivo,
    double Score) : IRequest;

public class RegistrarPresencaSessaoTreinamentoCommandValidator : AbstractValidator<RegistrarPresencaSessaoTreinamentoCommand>
{
    public RegistrarPresencaSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.SessaoTreinamentoId).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.DispositivoId).NotEmpty();
        RuleFor(x => x.SegredoDispositivo).NotEmpty();
        RuleFor(x => x.Score).InclusiveBetween(0, 100);
    }
}

public class RegistrarPresencaSessaoTreinamentoCommandHandler : IRequestHandler<RegistrarPresencaSessaoTreinamentoCommand>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoBiometriaLocalService _autenticacao;

    public RegistrarPresencaSessaoTreinamentoCommandHandler(IAppDbContext db, IAutenticacaoBiometriaLocalService autenticacao)
    {
        _db = db;
        _autenticacao = autenticacao;
    }

    public async Task Handle(RegistrarPresencaSessaoTreinamentoCommand request, CancellationToken ct)
    {
        var participante = await _db.ParticipantesSessaoTreinamento
            .FirstOrDefaultAsync(p => p.SessaoTreinamentoId == request.SessaoTreinamentoId && p.TrabalhadorId == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Este trabalhador não está inscrito nesta turma.");

        if (participante.PresencaConfirmadaEm is not null)
            throw new InvalidOperationException("Presença já confirmada para este participante.");

        await _autenticacao.AutenticarPorMatchLocalAsync(
            request.DispositivoId, request.SegredoDispositivo, request.TrabalhadorId, request.Score, ct);

        participante.PresencaConfirmadaEm = DateTime.UtcNow;
        participante.ScoreConfianca = request.Score;

        await _db.SaveChangesAsync(ct);
    }
}
