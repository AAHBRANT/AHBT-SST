using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pgrs.Commands;

public record ExcluirPgrCommand(Guid Id) : IRequest;

public class ExcluirPgrCommandValidator : AbstractValidator<ExcluirPgrCommand>
{
    public ExcluirPgrCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirPgrCommandHandler : IRequestHandler<ExcluirPgrCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirPgrCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirPgrCommand request, CancellationToken ct)
    {
        var pgr = await _db.Pgrs.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"PGR {request.Id} não encontrado.");

        _db.Pgrs.Remove(pgr);
        await _db.SaveChangesAsync(ct);
    }
}
