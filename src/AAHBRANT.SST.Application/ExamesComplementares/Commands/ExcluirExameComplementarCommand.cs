using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.ExamesComplementares.Commands;

public record ExcluirExameComplementarCommand(Guid Id) : IRequest;

public class ExcluirExameComplementarCommandValidator : AbstractValidator<ExcluirExameComplementarCommand>
{
    public ExcluirExameComplementarCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirExameComplementarCommandHandler : IRequestHandler<ExcluirExameComplementarCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirExameComplementarCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirExameComplementarCommand request, CancellationToken ct)
    {
        var exame = await _db.ExamesComplementares.FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Exame complementar {request.Id} não encontrado.");

        _db.ExamesComplementares.Remove(exame);
        await _db.SaveChangesAsync(ct);
    }
}
