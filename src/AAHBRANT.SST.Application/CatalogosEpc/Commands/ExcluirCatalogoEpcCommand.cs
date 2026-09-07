using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpc.Commands;

public record ExcluirCatalogoEpcCommand(Guid Id) : IRequest;

public class ExcluirCatalogoEpcCommandHandler : IRequestHandler<ExcluirCatalogoEpcCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirCatalogoEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirCatalogoEpcCommand request, CancellationToken ct)
    {
        var item = await _db.CatalogoEpcs.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Item de EPC não encontrado.");
        item.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}
