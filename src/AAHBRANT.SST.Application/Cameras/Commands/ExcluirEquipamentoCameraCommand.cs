using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Exclusão lógica (Ativo=false). Recusa quando ainda há câmera em uso apontando para o equipamento: a câmera deixaria
// de receber imagem em silêncio. A política "só Administrador" é aplicada no controller (policy do piloto).
public record ExcluirEquipamentoCameraCommand(Guid Id) : IRequest;

public class ExcluirEquipamentoCameraCommandHandler : IRequestHandler<ExcluirEquipamentoCameraCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirEquipamentoCameraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirEquipamentoCameraCommand request, CancellationToken ct)
    {
        var equipamento = await _db.EquipamentosCamera.FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Equipamento {request.Id} não encontrado.");

        var emUso = await _db.Cameras.CountAsync(c => c.EquipamentoCameraId == equipamento.Id, ct);
        if (emUso > 0)
            throw new InvalidOperationException(
                $"Há {emUso} câmera(s) usando este equipamento. Exclua essas câmeras ou aponte-as para outro equipamento antes.");

        equipamento.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}
