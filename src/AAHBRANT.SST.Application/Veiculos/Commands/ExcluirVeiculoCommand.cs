using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Veiculos.Commands;

// Exclusão lógica (Ativo=false): o histórico de inspeções continua preservado no banco. A
// política "só Administrador" é aplicada no controller (PoliticasAutorizacao.SomenteAdministrador).
public record ExcluirVeiculoCommand(Guid Id) : IRequest;

public class ExcluirVeiculoCommandHandler : IRequestHandler<ExcluirVeiculoCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirVeiculoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirVeiculoCommand request, CancellationToken ct)
    {
        var veiculo = await _db.Veiculos.FirstOrDefaultAsync(v => v.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Veículo {request.Id} não encontrado.");

        if (await _db.Inspecoes.AnyAsync(i => i.VeiculoId == veiculo.Id && i.Status == StatusInspecao.EmAndamento, ct))
            throw new InvalidOperationException(
                "Não é possível excluir o veículo com uma inspeção em andamento. Conclua ou exclua a inspeção primeiro.");

        veiculo.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}
