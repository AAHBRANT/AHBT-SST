using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados;

// Regra de bloqueio de liberação do módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-
// terceirizado-design.md §8) — lista vazia significa "sem pendência" (pessoa Liberada).
public static class CalculadoraLiberacaoTerceirizado
{
    public static async Task<List<string>> ObterPendenciasAsync(
        IAppDbContext db, Guid trabalhadorId, Guid funcaoId, CancellationToken ct = default)
    {
        var pendencias = new List<string>();
        var hoje = DateTime.UtcNow.Date;
        var funcaoNome = await db.Funcoes
            .Where(f => f.Id == funcaoId)
            .Select(f => f.Nome)
            .FirstOrDefaultAsync(ct);
        var isentoTreinamentos = FuncaoSstClassifier.EhTecnicoSeguranca(funcaoNome);

        var asoValido = await db.Asos.AnyAsync(a => a.TrabalhadorId == trabalhadorId
            && (a.ResultadoStatus == ResultadoAso.Apto || a.ResultadoStatus == ResultadoAso.AptoComRestricao)
            && a.DataValidade.Date >= hoje, ct);
        if (!asoValido) pendencias.Add("ASO válido pendente");

        if (!isentoTreinamentos)
        {
            var cursoIntegracao = await db.CursosTreinamento.FirstOrDefaultAsync(c => c.EhIntegracaoSeguranca, ct);
            if (cursoIntegracao is null)
            {
                pendencias.Add("Curso de Integração de Segurança não configurado no catálogo");
            }
            else
            {
                var integracaoValida = await db.Treinamentos.AnyAsync(t => t.TrabalhadorId == trabalhadorId
                    && t.CursoTreinamentoId == cursoIntegracao.Id && t.DataValidade.Date >= hoje, ct);
                if (!integracaoValida) pendencias.Add("Integração de Segurança pendente");
            }

            var treinamentosObrigatorios = await db.MatrizTreinamentoFuncoes
                .Where(m => m.FuncaoId == funcaoId)
                .Select(m => new { m.CursoTreinamentoId, Nome = m.CursoTreinamento!.Nome })
                .ToListAsync(ct);
            foreach (var curso in treinamentosObrigatorios)
            {
                var valido = await db.Treinamentos.AnyAsync(t => t.TrabalhadorId == trabalhadorId
                    && t.CursoTreinamentoId == curso.CursoTreinamentoId && t.DataValidade.Date >= hoje, ct);
                if (!valido) pendencias.Add($"Treinamento obrigatório pendente: {curso.Nome}");
            }
        }

        var episObrigatorios = await db.MatrizEpiFuncoes
            .Where(m => m.FuncaoId == funcaoId)
            .Select(m => new { m.CatalogoEpiId, Nome = m.CatalogoEpi!.Nome })
            .ToListAsync(ct);
        foreach (var epi in episObrigatorios)
        {
            // Confirmada=true E ainda não devolvida — um EPI já devolvido (DataDevolucao preenchida)
            // não satisfaz mais a exigência da função, mesmo tendo sido confirmado no passado
            // (correção feita após revisão técnica: a primeira versão só checava Confirmada).
            var confirmadaEEmPosse = await db.EntregasEpi.AnyAsync(e => e.TrabalhadorId == trabalhadorId
                && e.CatalogoEpiId == epi.CatalogoEpiId && e.Confirmada && e.DataDevolucao == null, ct);
            if (confirmadaEEmPosse) continue;

            var reservada = await db.EntregasEpi.AnyAsync(e => e.TrabalhadorId == trabalhadorId
                && e.CatalogoEpiId == epi.CatalogoEpiId && !e.Confirmada, ct);
            pendencias.Add(reservada
                ? $"EPI reservado, aguardando confirmação de entrega: {epi.Nome}"
                : $"EPI não reservado (sem estoque): {epi.Nome}");
        }

        return pendencias;
    }
}
