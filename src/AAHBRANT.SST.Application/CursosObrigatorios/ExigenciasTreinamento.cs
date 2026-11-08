using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosObrigatorios;

// Quais cursos uma função precisa NAQUELA obra (08/11/2026): a fonte é o PGR — os cursos obrigatórios aprovados da obra
// (Treinamentos › Cursos obrigatórios por obra). A Matriz de Treinamento por Função só vale como reserva para obra que
// ainda não tem nenhum curso aprovado pelo PGR (PGR importado/legado). Mesma regra que a aptidão do Início já usava,
// agora num lugar só para OS, liberação, terceirizados e plano de integração.
public static class ExigenciasTreinamento
{
    public static async Task<List<Guid>> CursosAsync(IAppDbContext db, Guid? obraId, Guid funcaoId, CancellationToken ct)
    {
        if (obraId is { } obra)
        {
            var aprovados = await db.CursosObrigatoriosFuncaoObra
                .Where(c => c.ObraId == obra && c.Status == StatusCursoObrigatorio.Aprovado)
                .Select(c => new { c.FuncaoId, c.CursoTreinamentoId })
                .ToListAsync(ct);
            if (aprovados.Count > 0)
                return aprovados.Where(c => c.FuncaoId == funcaoId).Select(c => c.CursoTreinamentoId).Distinct().ToList();
        }
        return await db.MatrizTreinamentoFuncoes.Where(m => m.FuncaoId == funcaoId)
            .Select(m => m.CursoTreinamentoId).Distinct().ToListAsync(ct);
    }

    // Versão em lote: carrega uma vez e responde por (obra, função).
    public static async Task<Func<Guid, Guid, List<Guid>?>> CarregarAsync(IAppDbContext db, IEnumerable<Guid> obraIds, CancellationToken ct)
    {
        var ids = obraIds.Distinct().ToList();
        var porObraFuncao = (await db.CursosObrigatoriosFuncaoObra
                .Where(c => ids.Contains(c.ObraId) && c.Status == StatusCursoObrigatorio.Aprovado)
                .Select(c => new { c.ObraId, c.FuncaoId, c.CursoTreinamentoId })
                .ToListAsync(ct))
            .GroupBy(c => (c.ObraId, c.FuncaoId))
            .ToDictionary(g => g.Key, g => g.Select(c => c.CursoTreinamentoId).Distinct().ToList());
        var obrasComPgr = porObraFuncao.Keys.Select(k => k.ObraId).ToHashSet();
        var porFuncao = (await db.MatrizTreinamentoFuncoes.Select(m => new { m.FuncaoId, m.CursoTreinamentoId }).ToListAsync(ct))
            .GroupBy(m => m.FuncaoId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.CursoTreinamentoId).Distinct().ToList());

        return (obraId, funcaoId) => obrasComPgr.Contains(obraId)
            ? porObraFuncao.GetValueOrDefault((obraId, funcaoId))
            : porFuncao.GetValueOrDefault(funcaoId);
    }
}
