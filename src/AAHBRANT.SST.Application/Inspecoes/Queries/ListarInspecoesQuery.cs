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

    // Projeção direta via Select em vez de Include de Obra/Atividade/ChecklistModelo/
    // ResponsavelUsuario/Respostas: o Include de Respostas (potencialmente muitas linhas por
    // inspeção) junto dos demais Includes de referência causava explosão cartesiana no SQL gerado
    // — pesado justamente na chamada sem filtro de obra feita pelo dashboard. Select gera uma
    // subconsulta correlacionada só para os contadores de Respostas, sem duplicar linhas.
    public async Task<List<InspecaoDto>> Handle(ListarInspecoesQuery request, CancellationToken ct)
    {
        var query = _db.Inspecoes.AsNoTracking().AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(i => i.ObraId == request.ObraId.Value);

        return await query
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new InspecaoDto
            {
                Id = i.Id,
                TipoInspecao = i.TipoInspecao,
                ObraId = i.ObraId,
                ObraNome = i.Obra != null ? i.Obra.Nome : string.Empty,
                AtividadeId = i.AtividadeId,
                AtividadeNome = i.Atividade != null ? i.Atividade.Nome : null,
                ChecklistModeloId = i.ChecklistModeloId,
                ChecklistModeloNome = i.ChecklistModelo != null ? i.ChecklistModelo.Nome : string.Empty,
                ChecklistModeloVersao = i.ChecklistModelo != null ? i.ChecklistModelo.Versao : 0,
                Data = i.Data,
                ResponsavelUsuarioId = i.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = i.ResponsavelUsuario != null ? i.ResponsavelUsuario.Nome : string.Empty,
                Status = i.Status,
                TotalItens = i.Respostas.Count,
                ItensRespondidos = i.Respostas.Count(r => r.StatusItem != null),
                ItensNaoConformes = i.Respostas.Count(r => r.StatusItem == StatusItemChecklist.NaoConforme),
            })
            .ToListAsync(ct);
    }
}
