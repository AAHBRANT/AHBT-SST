using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Vínculo manual do trabalhador pelo técnico. TrabalhadorId nulo remove o vínculo.
public record VincularTrabalhadorOcorrenciaEpiCommand(Guid Id, Guid? TrabalhadorId, string? AzureAdObjectId) : IRequest;

public class VincularTrabalhadorOcorrenciaEpiCommandHandler : IRequestHandler<VincularTrabalhadorOcorrenciaEpiCommand>
{
    private readonly IAppDbContext _db;
    public VincularTrabalhadorOcorrenciaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(VincularTrabalhadorOcorrenciaEpiCommand request, CancellationToken ct)
    {
        var ocorrencia = await _db.OcorrenciasEpiCamera.FirstOrDefaultAsync(o => o.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Ocorrência {request.Id} não encontrada.");

        if (request.TrabalhadorId is not { } trabalhadorId)
        {
            ocorrencia.TrabalhadorId = null;
            ocorrencia.VinculadoPorUsuarioId = null;
            ocorrencia.VinculadoEmUtc = null;
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (!await _db.Trabalhadores.AnyAsync(t => t.Id == trabalhadorId, ct))
            throw new KeyNotFoundException("Trabalhador não encontrado.");

        ocorrencia.TrabalhadorId = trabalhadorId;
        ocorrencia.VinculadoEmUtc = DateTime.UtcNow;
        ocorrencia.VinculadoPorUsuarioId = string.IsNullOrWhiteSpace(request.AzureAdObjectId)
            ? null
            : await _db.Usuarios.Where(u => u.AzureAdObjectId == request.AzureAdObjectId).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        if (ocorrencia.Situacao == SituacaoOcorrenciaEpi.Nova)
            ocorrencia.Situacao = SituacaoOcorrenciaEpi.Conferida;

        await _db.SaveChangesAsync(ct);
    }
}
