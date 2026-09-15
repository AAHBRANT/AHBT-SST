using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Commands;

public record ExcluirCatalogoUniformeCommand(Guid Id) : IRequest;

public class ExcluirCatalogoUniformeCommandHandler : IRequestHandler<ExcluirCatalogoUniformeCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirCatalogoUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirCatalogoUniformeCommand request, CancellationToken ct)
    {
        var item = await _db.CatalogoUniformes.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Peça de uniforme não encontrada.");
        item.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}
