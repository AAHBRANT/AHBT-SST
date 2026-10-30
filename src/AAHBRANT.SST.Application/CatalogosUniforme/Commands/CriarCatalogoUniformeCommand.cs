using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Commands;

public record CriarCatalogoUniformeCommand(string Nome, string? Categoria, string? ReferenciaVisual = null) : IRequest<Guid>;

public class CriarCatalogoUniformeCommandValidator : AbstractValidator<CriarCatalogoUniformeCommand>
{
    public CriarCatalogoUniformeCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Categoria).MaximumLength(100);
        RuleFor(x => x.ReferenciaVisual).Must(ReferenciasUniforme.Valida).WithMessage("Modelo visual de uniforme inválido.");
    }
}

public class CriarCatalogoUniformeCommandHandler : IRequestHandler<CriarCatalogoUniformeCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarCatalogoUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarCatalogoUniformeCommand request, CancellationToken ct)
    {
        var item = new Domain.Entidades.CatalogoUniforme { Nome = NomeUniforme.Padronizar(request.Nome), Categoria = request.Categoria, ReferenciaVisual = request.ReferenciaVisual };
        _db.CatalogoUniformes.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }
}
