using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Asos.Commands;

public record ExcluirAsoCommand(Guid Id) : IRequest;

public class ExcluirAsoCommandValidator : AbstractValidator<ExcluirAsoCommand>
{
    public ExcluirAsoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirAsoCommandHandler : IRequestHandler<ExcluirAsoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirAsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirAsoCommand request, CancellationToken ct)
    {
        var aso = await _db.Asos.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"ASO {request.Id} não encontrado.");

        _db.Asos.Remove(aso);
        await _db.SaveChangesAsync(ct);
    }
}
