using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AprEtapaRiscos.Commands;

public record ExcluirAprEtapaRiscoCommand(Guid Id) : IRequest;

public class ExcluirAprEtapaRiscoCommandValidator : AbstractValidator<ExcluirAprEtapaRiscoCommand>
{
    public ExcluirAprEtapaRiscoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirAprEtapaRiscoCommandHandler : IRequestHandler<ExcluirAprEtapaRiscoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirAprEtapaRiscoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirAprEtapaRiscoCommand request, CancellationToken ct)
    {
        var risco = await _db.AprEtapaRiscos.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Risco de etapa de APR {request.Id} não encontrado.");

        var aprId = await _db.AprEtapas.Where(e => e.Id == risco.AprEtapaId).Select(e => e.AprId).FirstOrDefaultAsync(ct);
        await _db.GarantirAprNoEscopoAsync(aprId, ct);

        _db.AprEtapaRiscos.Remove(risco);
        await _db.SaveChangesAsync(ct);
    }
}
