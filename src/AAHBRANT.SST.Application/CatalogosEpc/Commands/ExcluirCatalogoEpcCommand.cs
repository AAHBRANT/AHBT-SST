using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpc.Commands;

public record ExcluirCatalogoEpcCommand(Guid Id) : IRequest;

public class ExcluirCatalogoEpcCommandValidator : AbstractValidator<ExcluirCatalogoEpcCommand>
{
    public ExcluirCatalogoEpcCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirCatalogoEpcCommandHandler : IRequestHandler<ExcluirCatalogoEpcCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirCatalogoEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirCatalogoEpcCommand request, CancellationToken ct)
    {
        var epc = await _db.CatalogoEpcs.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("EPC de catálogo não encontrado.");

        _db.CatalogoEpcs.Remove(epc);
        await _db.SaveChangesAsync(ct);
    }
}
