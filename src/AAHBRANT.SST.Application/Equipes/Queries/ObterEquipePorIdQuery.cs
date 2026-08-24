using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Equipes.Queries;

public record ObterEquipePorIdQuery(Guid Id) : IRequest<EquipeDto?>;

public class ObterEquipePorIdQueryHandler : IRequestHandler<ObterEquipePorIdQuery, EquipeDto?>
{
    private readonly IAppDbContext _db;

    public ObterEquipePorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EquipeDto?> Handle(ObterEquipePorIdQuery request, CancellationToken ct)
    {
        return await _db.Equipes
            .Where(e => e.Id == request.Id)
            .Select(e => new EquipeDto
            {
                Id = e.Id,
                SetorId = e.SetorId,
                SetorNome = e.Setor!.Nome,
                ObraId = e.Setor.ObraId,
                ObraNome = e.Setor.Obra!.Nome,
                Nome = e.Nome,
                EncarregadoId = e.EncarregadoId,
                EncarregadoNome = e.Encarregado != null ? e.Encarregado.Nome : null,
                QuantidadeTrabalhadores = e.Trabalhadores.Count(t => t.Ativo)
            })
            .FirstOrDefaultAsync(ct);
    }
}
