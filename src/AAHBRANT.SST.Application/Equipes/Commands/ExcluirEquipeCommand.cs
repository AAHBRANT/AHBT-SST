using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Equipes.Commands;

public record ExcluirEquipeCommand(Guid Id) : IRequest;

public class ExcluirEquipeCommandValidator : AbstractValidator<ExcluirEquipeCommand>
{
    public ExcluirEquipeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirEquipeCommandHandler : IRequestHandler<ExcluirEquipeCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirEquipeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirEquipeCommand request, CancellationToken ct)
    {
        var equipe = await _db.Equipes.FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Equipe {request.Id} não encontrada.");

        _db.Equipes.Remove(equipe);
        await _db.SaveChangesAsync(ct);
    }
}
