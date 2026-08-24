using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Riscos.Commands;

public record ExcluirRiscoCommand(Guid Id) : IRequest;

public class ExcluirRiscoCommandValidator : AbstractValidator<ExcluirRiscoCommand>
{
    public ExcluirRiscoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirRiscoCommandHandler : IRequestHandler<ExcluirRiscoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirRiscoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirRiscoCommand request, CancellationToken ct)
    {
        var risco = await _db.Riscos.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Risco {request.Id} não encontrado.");

        _db.Riscos.Remove(risco);
        await _db.SaveChangesAsync(ct);
    }
}
