using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

public record InscreverCandidatoCipaCommand(Guid ProcessoEleitoralId, Guid TrabalhadorId) : IRequest<Guid>;

public class InscreverCandidatoCipaCommandValidator : AbstractValidator<InscreverCandidatoCipaCommand>
{
    public InscreverCandidatoCipaCommandValidator()
    {
        RuleFor(x => x.ProcessoEleitoralId).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
    }
}

public class InscreverCandidatoCipaCommandHandler : IRequestHandler<InscreverCandidatoCipaCommand, Guid>
{
    private readonly IAppDbContext _db;

    public InscreverCandidatoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(InscreverCandidatoCipaCommand request, CancellationToken ct)
    {
        var processo = await _db.ProcessosEleitoraisCipa.FirstOrDefaultAsync(p => p.Id == request.ProcessoEleitoralId, ct)
            ?? throw new KeyNotFoundException($"Processo eleitoral {request.ProcessoEleitoralId} não encontrado.");

        if (!await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId && t.ObraId == processo.ObraId, ct))
            throw new KeyNotFoundException("Trabalhador não encontrado ou não pertence à obra deste processo eleitoral.");

        var jaInscrito = await _db.CandidatosCipa.AnyAsync(
            c => c.ProcessoEleitoralId == request.ProcessoEleitoralId && c.TrabalhadorId == request.TrabalhadorId && c.Ativo, ct);
        if (jaInscrito)
            throw new InvalidOperationException("Este trabalhador já está inscrito neste processo eleitoral.");

        var candidato = new CandidatoCipa
        {
            ProcessoEleitoralId = request.ProcessoEleitoralId,
            TrabalhadorId = request.TrabalhadorId,
            DataInscricao = DateTime.UtcNow,
            Status = StatusCandidatoCipa.Inscrito,
        };

        _db.CandidatosCipa.Add(candidato);

        if (processo.Status == StatusProcessoEleitoralCipa.Convocado)
            processo.Status = StatusProcessoEleitoralCipa.InscricoesAbertas;

        await _db.SaveChangesAsync(ct);
        return candidato.Id;
    }
}
