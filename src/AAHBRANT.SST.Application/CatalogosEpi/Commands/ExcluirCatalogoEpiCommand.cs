using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpi.Commands;

public record ExcluirCatalogoEpiCommand(Guid Id) : IRequest;

public class ExcluirCatalogoEpiCommandValidator : AbstractValidator<ExcluirCatalogoEpiCommand>
{
    public ExcluirCatalogoEpiCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirCatalogoEpiCommandHandler : IRequestHandler<ExcluirCatalogoEpiCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirCatalogoEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirCatalogoEpiCommand request, CancellationToken ct)
    {
        var epi = await _db.CatalogoEpis.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("EPI de catálogo não encontrado.");

        _db.CatalogoEpis.Remove(epi);
        await _db.SaveChangesAsync(ct);
    }
}
