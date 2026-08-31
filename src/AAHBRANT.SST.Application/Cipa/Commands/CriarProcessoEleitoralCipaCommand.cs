using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

public record CriarProcessoEleitoralCipaCommand(
    Guid ObraId,
    string? NumeroDocumento,
    DateTime DataConvocacao,
    DateTime DataInicioInscricoes,
    DateTime DataFimInscricoes,
    DateTime DataVotacao) : IRequest<Guid>;

public class CriarProcessoEleitoralCipaCommandValidator : AbstractValidator<CriarProcessoEleitoralCipaCommand>
{
    public CriarProcessoEleitoralCipaCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.NumeroDocumento).MaximumLength(50);
        RuleFor(x => x.DataFimInscricoes).GreaterThanOrEqualTo(x => x.DataInicioInscricoes)
            .WithMessage("O fim das inscrições precisa ser depois do início.");
        RuleFor(x => x.DataVotacao).GreaterThanOrEqualTo(x => x.DataFimInscricoes)
            .WithMessage("A votação precisa ser depois do fim das inscrições.");
    }
}

public class CriarProcessoEleitoralCipaCommandHandler : IRequestHandler<CriarProcessoEleitoralCipaCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarProcessoEleitoralCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarProcessoEleitoralCipaCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var processo = new ProcessoEleitoralCipa
        {
            ObraId = request.ObraId,
            NumeroDocumento = request.NumeroDocumento,
            DataConvocacao = request.DataConvocacao,
            DataInicioInscricoes = request.DataInicioInscricoes,
            DataFimInscricoes = request.DataFimInscricoes,
            DataVotacao = request.DataVotacao,
            Status = StatusProcessoEleitoralCipa.Convocado,
        };

        _db.ProcessosEleitoraisCipa.Add(processo);
        await _db.SaveChangesAsync(ct);
        return processo.Id;
    }
}
