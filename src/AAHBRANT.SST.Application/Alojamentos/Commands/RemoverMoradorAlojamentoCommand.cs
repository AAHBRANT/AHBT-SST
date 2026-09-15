using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Commands;

public record RemoverMoradorAlojamentoCommand(Guid AlojamentoMoradorId) : IRequest;

public class RemoverMoradorAlojamentoCommandHandler : IRequestHandler<RemoverMoradorAlojamentoCommand>
{
    private readonly IAppDbContext _db;
    public RemoverMoradorAlojamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RemoverMoradorAlojamentoCommand request, CancellationToken ct)
    {
        var morador = await _db.AlojamentoMoradores.FirstOrDefaultAsync(m => m.Id == request.AlojamentoMoradorId, ct)
            ?? throw new KeyNotFoundException($"Vínculo {request.AlojamentoMoradorId} não encontrado.");

        morador.DataSaida = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
