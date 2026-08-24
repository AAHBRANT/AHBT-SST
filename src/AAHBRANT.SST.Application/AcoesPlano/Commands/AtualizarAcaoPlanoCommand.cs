using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AcoesPlano.Commands;

public record AtualizarAcaoPlanoCommand(
    Guid Id,
    TipoAcaoPlano Tipo,
    string Descricao,
    Guid? ResponsavelUsuarioId,
    PrioridadeAcao Prioridade,
    DateTime? Prazo,
    StatusControleRisco Status,
    DateTime? DataConclusao) : IRequest;

public class AtualizarAcaoPlanoCommandValidator : AbstractValidator<AtualizarAcaoPlanoCommand>
{
    public AtualizarAcaoPlanoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(500);
    }
}

public class AtualizarAcaoPlanoCommandHandler : IRequestHandler<AtualizarAcaoPlanoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarAcaoPlanoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarAcaoPlanoCommand request, CancellationToken ct)
    {
        var acao = await _db.AcoesPlano.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Ação de plano {request.Id} não encontrada.");

        acao.Tipo = request.Tipo;
        acao.Descricao = request.Descricao;
        acao.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
        acao.Prioridade = request.Prioridade;
        acao.Prazo = request.Prazo;
        acao.Status = request.Status;
        acao.DataConclusao = request.DataConclusao;

        await _db.SaveChangesAsync(ct);
    }
}
