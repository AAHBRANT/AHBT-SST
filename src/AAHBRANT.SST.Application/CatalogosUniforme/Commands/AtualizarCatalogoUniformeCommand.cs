using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Commands;

public record AtualizarCatalogoUniformeCommand(Guid Id, string Nome, string? Categoria) : IRequest;

public class AtualizarCatalogoUniformeCommandValidator : AbstractValidator<AtualizarCatalogoUniformeCommand>
{
    public AtualizarCatalogoUniformeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Categoria).MaximumLength(100);
    }
}

public class AtualizarCatalogoUniformeCommandHandler : IRequestHandler<AtualizarCatalogoUniformeCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarCatalogoUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarCatalogoUniformeCommand request, CancellationToken ct)
    {
        var item = await _db.CatalogoUniformes.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Peça de uniforme não encontrada.");
        item.Nome = request.Nome;
        item.Categoria = request.Categoria;
        await _db.SaveChangesAsync(ct);
    }
}
