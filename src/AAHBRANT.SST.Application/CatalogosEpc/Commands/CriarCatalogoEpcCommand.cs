using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.CatalogosEpc.Commands;

public record CriarCatalogoEpcCommand(string Nome, string? Categoria) : IRequest<Guid>;

public class CriarCatalogoEpcCommandValidator : AbstractValidator<CriarCatalogoEpcCommand>
{
    public CriarCatalogoEpcCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Categoria).MaximumLength(100);
    }
}

public class CriarCatalogoEpcCommandHandler : IRequestHandler<CriarCatalogoEpcCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarCatalogoEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarCatalogoEpcCommand request, CancellationToken ct)
    {
        var item = new Domain.Entidades.CatalogoEpc { Nome = request.Nome, Categoria = request.Categoria };
        _db.CatalogoEpcs.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }
}
