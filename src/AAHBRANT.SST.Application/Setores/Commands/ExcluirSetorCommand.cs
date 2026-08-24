using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Setores.Commands;

public record ExcluirSetorCommand(Guid Id) : IRequest;

public class ExcluirSetorCommandValidator : AbstractValidator<ExcluirSetorCommand>
{
    public ExcluirSetorCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirSetorCommandHandler : IRequestHandler<ExcluirSetorCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirSetorCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirSetorCommand request, CancellationToken ct)
    {
        var setor = await _db.Setores.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Setor {request.Id} não encontrado.");

        _db.Setores.Remove(setor);
        await _db.SaveChangesAsync(ct);
    }
}
