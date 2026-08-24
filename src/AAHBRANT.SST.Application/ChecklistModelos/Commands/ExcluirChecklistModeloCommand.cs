using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.ChecklistModelos.Commands;

public record ExcluirChecklistModeloCommand(Guid Id) : IRequest;

public class ExcluirChecklistModeloCommandValidator : AbstractValidator<ExcluirChecklistModeloCommand>
{
    public ExcluirChecklistModeloCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirChecklistModeloCommandHandler : IRequestHandler<ExcluirChecklistModeloCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirChecklistModeloCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirChecklistModeloCommand request, CancellationToken ct)
    {
        var checklist = await _db.ChecklistModelos.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Checklist {request.Id} não encontrado.");

        _db.ChecklistModelos.Remove(checklist);
        await _db.SaveChangesAsync(ct);
    }
}
