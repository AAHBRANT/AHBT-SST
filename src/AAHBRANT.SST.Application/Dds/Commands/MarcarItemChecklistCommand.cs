using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

public record MarcarItemChecklistCommand(Guid ItemId, bool Verificado) : IRequest;

public class MarcarItemChecklistCommandValidator : AbstractValidator<MarcarItemChecklistCommand>
{
    public MarcarItemChecklistCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
    }
}

public class MarcarItemChecklistCommandHandler : IRequestHandler<MarcarItemChecklistCommand>
{
    private readonly IAppDbContext _db;

    public MarcarItemChecklistCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(MarcarItemChecklistCommand request, CancellationToken ct)
    {
        var item = await _db.DdsItensChecklist.FirstOrDefaultAsync(i => i.Id == request.ItemId, ct)
            ?? throw new KeyNotFoundException($"Item de checklist {request.ItemId} não encontrado.");

        item.Verificado = request.Verificado;
        await _db.SaveChangesAsync(ct);
    }
}
