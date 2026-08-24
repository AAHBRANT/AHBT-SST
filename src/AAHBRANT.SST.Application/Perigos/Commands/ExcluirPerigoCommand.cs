using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Perigos.Commands;

public record ExcluirPerigoCommand(Guid Id) : IRequest;

public class ExcluirPerigoCommandValidator : AbstractValidator<ExcluirPerigoCommand>
{
    public ExcluirPerigoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirPerigoCommandHandler : IRequestHandler<ExcluirPerigoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirPerigoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirPerigoCommand request, CancellationToken ct)
    {
        var perigo = await _db.Perigos.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Perigo {request.Id} não encontrado.");

        _db.Perigos.Remove(perigo);
        await _db.SaveChangesAsync(ct);
    }
}
