using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Matriz de EPI por função da obra, criada ao aprovar o PGR (pedido do usuário, 04/11/2026, "caminho 2"): o tipo de EPI que o
// PGR exige para a função (EpisDoPgr) é ligado a um item que JÁ existe no catálogo da obra — ou, na falta dele, ao item
// antigo compartilhado. O que não tem item correspondente não entra: vira ação no plano do PGR para o técnico cadastrar o item
// da obra (com o CA dele). Nada é inventado e nenhum item é criado no catálogo. Só acrescenta: ligação que o técnico já
// montou na obra nunca é removida, e a ligação desativada é reativada.
public static class MatrizEpiDoPgr
{
    // Devolve as ações a incluir no plano: uma por tipo de EPI sem item no catálogo, citando as funções que o exigem.
    public static async Task<List<string>> AplicarAsync(
        IAppDbContext db, EstruturaGeracaoPgr estrutura, Guid obraId,
        IReadOnlyDictionary<string, Funcao> funcoesPorNome, CancellationToken ct)
    {
        var catalogo = (await db.CatalogoEpis.Where(c => c.ObraId == obraId || c.ObraId == null)
                .Select(c => new { c.Id, c.Nome, c.ObraId }).ToListAsync(ct))
            .Select(c => (c.Id, c.Nome, DaObra: c.ObraId == obraId, Tipos: EpisDoPgr.Reconhecer(c.Nome).Select(t => t.Nome).ToHashSet()))
            .ToList();

        var existentes = await db.MatrizEpiFuncoes.IgnoreQueryFilters()
            .Where(m => m.ObraId == obraId).ToListAsync(ct);
        var jaLigados = existentes.ToDictionary(m => (m.FuncaoId, m.CatalogoEpiId));

        var semItem = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var linha in RelacaoEpisPorFuncao.Montar(estrutura))
        {
            // Texto de medida que não cita um tipo conhecido não tem como ser casado: segue só na relação do PDF.
            if (EpisDoPgr.Ordem(linha.Epi) == int.MaxValue) continue;
            if (!funcoesPorNome.TryGetValue(FuncaoSstClassifier.Normalizar(linha.Funcao), out var funcao)) continue;

            var item = catalogo.Where(c => c.Tipos.Contains(linha.Epi))
                .OrderByDescending(c => c.DaObra).ThenBy(c => c.Nome).Select(c => (Guid?)c.Id).FirstOrDefault();
            if (item is null)
            {
                if (!semItem.TryGetValue(linha.Epi, out var funcoes)) semItem[linha.Epi] = funcoes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                funcoes.Add(linha.Funcao);
                continue;
            }

            if (jaLigados.TryGetValue((funcao.Id, item.Value), out var vinculo))
                vinculo.Ativo = true;
            else
            {
                var novo = new MatrizEpiFuncao { ObraId = obraId, Funcao = funcao, FuncaoId = funcao.Id, CatalogoEpiId = item.Value };
                db.MatrizEpiFuncoes.Add(novo);
                jaLigados[(funcao.Id, item.Value)] = novo;
            }
        }

        return semItem.OrderBy(x => EpisDoPgr.Ordem(x.Key))
            .Select(x => $"Cadastrar no catálogo de EPI da obra o item \"{x.Key}\", exigido pelo PGR para: {string.Join(", ", x.Value)}. Depois, ligue-o à função na matriz de EPI.")
            .ToList();
    }
}
