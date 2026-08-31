using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

public record ExcluirCatalogoTemaDdsCommand(Guid Id) : IRequest;

public class ExcluirCatalogoTemaDdsCommandValidator : AbstractValidator<ExcluirCatalogoTemaDdsCommand>
{
    public ExcluirCatalogoTemaDdsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirCatalogoTemaDdsCommandHandler : IRequestHandler<ExcluirCatalogoTemaDdsCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirCatalogoTemaDdsCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirCatalogoTemaDdsCommand request, CancellationToken ct)
    {
        var tema = await _db.CatalogosTemaDds.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Tema de DDS não encontrado.");

        _db.CatalogosTemaDds.Remove(tema);
        await _db.SaveChangesAsync(ct);
    }
}
