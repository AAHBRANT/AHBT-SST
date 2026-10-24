using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Commands;

public record CriarCatalogoUniformeCommand(string Nome, string? Categoria) : IRequest<Guid>;

public class CriarCatalogoUniformeCommandValidator : AbstractValidator<CriarCatalogoUniformeCommand>
{
    public CriarCatalogoUniformeCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Categoria).MaximumLength(100);
    }
}

public class CriarCatalogoUniformeCommandHandler : IRequestHandler<CriarCatalogoUniformeCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarCatalogoUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarCatalogoUniformeCommand request, CancellationToken ct)
    {
        var item = new Domain.Entidades.CatalogoUniforme { Nome = request.Nome, Categoria = request.Categoria };
        _db.CatalogoUniformes.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }
}
