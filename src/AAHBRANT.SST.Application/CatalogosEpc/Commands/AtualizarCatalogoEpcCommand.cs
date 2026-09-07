using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpc.Commands;

public record AtualizarCatalogoEpcCommand(Guid Id, string Nome, string? Categoria) : IRequest;

public class AtualizarCatalogoEpcCommandValidator : AbstractValidator<AtualizarCatalogoEpcCommand>
{
    public AtualizarCatalogoEpcCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Categoria).MaximumLength(100);
    }
}

public class AtualizarCatalogoEpcCommandHandler : IRequestHandler<AtualizarCatalogoEpcCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarCatalogoEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarCatalogoEpcCommand request, CancellationToken ct)
    {
        var item = await _db.CatalogoEpcs.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Item de EPC não encontrado.");
        item.Nome = request.Nome;
        item.Categoria = request.Categoria;
        await _db.SaveChangesAsync(ct);
    }
}
