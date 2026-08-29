using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RequisitosLegais.Commands;

public record ExcluirRequisitoLegalCommand(Guid Id) : IRequest;

public class ExcluirRequisitoLegalCommandValidator : AbstractValidator<ExcluirRequisitoLegalCommand>
{
    public ExcluirRequisitoLegalCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirRequisitoLegalCommandHandler : IRequestHandler<ExcluirRequisitoLegalCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirRequisitoLegalCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirRequisitoLegalCommand request, CancellationToken ct)
    {
        var requisito = await _db.RequisitosLegais.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Requisito legal {request.Id} não encontrado.");

        _db.RequisitosLegais.Remove(requisito);
        await _db.SaveChangesAsync(ct);
    }
}
