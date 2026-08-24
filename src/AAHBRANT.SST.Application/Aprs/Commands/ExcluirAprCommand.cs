using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Commands;

public record ExcluirAprCommand(Guid Id) : IRequest;

public class ExcluirAprCommandValidator : AbstractValidator<ExcluirAprCommand>
{
    public ExcluirAprCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirAprCommandHandler : IRequestHandler<ExcluirAprCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirAprCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirAprCommand request, CancellationToken ct)
    {
        var apr = await _db.Aprs.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"APR {request.Id} não encontrada.");

        _db.Aprs.Remove(apr);
        await _db.SaveChangesAsync(ct);
    }
}
