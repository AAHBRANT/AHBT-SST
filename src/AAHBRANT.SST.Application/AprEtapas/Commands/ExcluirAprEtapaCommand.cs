using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AprEtapas.Commands;

public record ExcluirAprEtapaCommand(Guid Id) : IRequest;

public class ExcluirAprEtapaCommandValidator : AbstractValidator<ExcluirAprEtapaCommand>
{
    public ExcluirAprEtapaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirAprEtapaCommandHandler : IRequestHandler<ExcluirAprEtapaCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirAprEtapaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirAprEtapaCommand request, CancellationToken ct)
    {
        var etapa = await _db.AprEtapas.FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Etapa de APR {request.Id} não encontrada.");

        _db.AprEtapas.Remove(etapa);
        await _db.SaveChangesAsync(ct);
    }
}
