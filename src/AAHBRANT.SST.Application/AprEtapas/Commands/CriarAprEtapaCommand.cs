using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AprEtapas.Commands;

// "ETAPA DA ATIVIDADE" (só Ordem/Descrição) — os perigos/riscos dessa etapa são adicionados depois,
// linha a linha, via CriarAprEtapaRiscoCommand (mesmo princípio de granularidade incremental já
// usado em outros módulos com child-collection, ex. CriarAcaoPlanoCommand).
public record CriarAprEtapaCommand(Guid AprId, int Ordem, string Descricao) : IRequest<Guid>;

public class CriarAprEtapaCommandValidator : AbstractValidator<CriarAprEtapaCommand>
{
    public CriarAprEtapaCommandValidator()
    {
        RuleFor(x => x.AprId).NotEmpty();
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(500);
    }
}

public class CriarAprEtapaCommandHandler : IRequestHandler<CriarAprEtapaCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAprEtapaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAprEtapaCommand request, CancellationToken ct)
    {
        var aprExiste = await _db.Aprs.AnyAsync(a => a.Id == request.AprId, ct);
        if (!aprExiste)
            throw new KeyNotFoundException($"APR {request.AprId} não encontrada.");

        var etapa = new AprEtapa
        {
            AprId = request.AprId,
            Ordem = request.Ordem,
            Descricao = request.Descricao,
        };

        _db.AprEtapas.Add(etapa);
        await _db.SaveChangesAsync(ct);
        return etapa.Id;
    }
}
