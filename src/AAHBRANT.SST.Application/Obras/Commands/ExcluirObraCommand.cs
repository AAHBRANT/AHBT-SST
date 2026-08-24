using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Obras.Commands;

public record ExcluirObraCommand(Guid Id) : IRequest;

public class ExcluirObraCommandValidator : AbstractValidator<ExcluirObraCommand>
{
    public ExcluirObraCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirObraCommandHandler : IRequestHandler<ExcluirObraCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirObraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirObraCommand request, CancellationToken ct)
    {
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Obra {request.Id} não encontrada.");

        _db.Obras.Remove(obra);
        await _db.SaveChangesAsync(ct);
    }
}
