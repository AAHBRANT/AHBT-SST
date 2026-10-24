using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Commands;

public record AdicionarMoradorAlojamentoCommand(Guid AlojamentoId, Guid TrabalhadorId) : IRequest<Guid>;

public class AdicionarMoradorAlojamentoCommandHandler : IRequestHandler<AdicionarMoradorAlojamentoCommand, Guid>
{
    private readonly IAppDbContext _db;
    public AdicionarMoradorAlojamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AdicionarMoradorAlojamentoCommand request, CancellationToken ct)
    {
        var alojamentoExiste = await _db.Alojamentos.AnyAsync(a => a.Id == request.AlojamentoId, ct);
        if (!alojamentoExiste)
            throw new KeyNotFoundException($"Alojamento {request.AlojamentoId} não encontrado.");

        var jaTemVinculoAtivo = await _db.AlojamentoMoradores
            .AnyAsync(m => m.TrabalhadorId == request.TrabalhadorId && m.DataSaida == null, ct);
        if (jaTemVinculoAtivo)
            throw new InvalidOperationException("Este trabalhador já tem um vínculo de alojamento ativo.");

        var morador = new AlojamentoMorador
        {
            AlojamentoId = request.AlojamentoId,
            TrabalhadorId = request.TrabalhadorId,
            DataDesde = DateTime.UtcNow,
        };
        _db.AlojamentoMoradores.Add(morador);
        await _db.SaveChangesAsync(ct);
        return morador.Id;
    }
}
