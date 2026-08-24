using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Queries;

public record ListarInspecoesQuery(Guid? ObraId = null) : IRequest<List<InspecaoDto>>;

public class ListarInspecoesQueryHandler : IRequestHandler<ListarInspecoesQuery, List<InspecaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarInspecoesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<InspecaoDto>> Handle(ListarInspecoesQuery request, CancellationToken ct)
    {
        var query = _db.Inspecoes
            .Include(i => i.Obra)
            .Include(i => i.Atividade)
            .Include(i => i.ChecklistModelo)
            .Include(i => i.ResponsavelUsuario)
            .Include(i => i.Respostas)
            .AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(i => i.ObraId == request.ObraId.Value);

        var inspecoes = await query.OrderByDescending(i => i.CreatedAtUtc).ToListAsync(ct);

        return inspecoes.Select(i =>
        {
            var respostasAtivas = i.Respostas.Where(r => r.Ativo).ToList();
            return new InspecaoDto
            {
                Id = i.Id,
                TipoInspecao = i.TipoInspecao,
                ObraId = i.ObraId,
                ObraNome = i.Obra?.Nome ?? string.Empty,
                AtividadeId = i.AtividadeId,
                AtividadeNome = i.Atividade?.Nome,
                ChecklistModeloId = i.ChecklistModeloId,
                ChecklistModeloNome = i.ChecklistModelo?.Nome ?? string.Empty,
                ChecklistModeloVersao = i.ChecklistModelo?.Versao ?? 0,
                Data = i.Data,
                ResponsavelUsuarioId = i.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = i.ResponsavelUsuario?.Nome ?? string.Empty,
                Status = i.Status,
                TotalItens = respostasAtivas.Count,
                ItensRespondidos = respostasAtivas.Count(r => r.StatusItem != null),
                ItensNaoConformes = respostasAtivas.Count(r => r.StatusItem == StatusItemChecklist.NaoConforme)
            };
        }).ToList();
    }
}
