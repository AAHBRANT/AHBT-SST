using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

public record AvaliarInscricaoCandidatoCipaCommand(Guid CandidatoId, bool Deferido, string? MotivoIndeferimento) : IRequest;

public class AvaliarInscricaoCandidatoCipaCommandValidator : AbstractValidator<AvaliarInscricaoCandidatoCipaCommand>
{
    public AvaliarInscricaoCandidatoCipaCommandValidator()
    {
        RuleFor(x => x.CandidatoId).NotEmpty();
        RuleFor(x => x.MotivoIndeferimento).NotEmpty().MaximumLength(500).When(x => !x.Deferido);
    }
}

public class AvaliarInscricaoCandidatoCipaCommandHandler : IRequestHandler<AvaliarInscricaoCandidatoCipaCommand>
{
    private readonly IAppDbContext _db;

    public AvaliarInscricaoCandidatoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AvaliarInscricaoCandidatoCipaCommand request, CancellationToken ct)
    {
        var candidato = await _db.CandidatosCipa.FirstOrDefaultAsync(c => c.Id == request.CandidatoId, ct)
            ?? throw new KeyNotFoundException($"Candidato {request.CandidatoId} não encontrado.");
        await _db.GarantirObraDoPaiNoEscopoAsync(
            _db.ProcessosEleitoraisCipa.IgnoreQueryFilters().Where(p => p.Id == candidato.ProcessoEleitoralId).Select(p => p.ObraId),
            $"Candidato {request.CandidatoId} não encontrado.", ct);

        candidato.Status = request.Deferido ? StatusCandidatoCipa.Deferido : StatusCandidatoCipa.Indeferido;
        candidato.MotivoIndeferimento = request.Deferido ? null : request.MotivoIndeferimento;

        await _db.SaveChangesAsync(ct);
    }
}
