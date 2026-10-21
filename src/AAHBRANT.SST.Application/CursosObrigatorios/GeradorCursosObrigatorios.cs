using System.Text.RegularExpressions;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosObrigatorios;

/// <param name="Sugeridos">Sugestões novas gravadas nesta geração.</param>
/// <param name="NormasSemCurso">NRs que a obra exige mas não têm curso no catálogo.</param>
public record ResultadoGeracaoCursosObrigatoriosDto(int Sugeridos, List<string> NormasSemCurso);

// Lê o inventário do PGR da obra (GHE → funções, atividades, perigos) e cruza com as regras
// risco → NR → curso. Só ACRESCENTA sugestões: o que já existe para a função (aprovado, recusado
// ou adicionado à mão) nunca é apagado nem sugerido de novo.
internal static class GeradorCursosObrigatorios
{
    private static readonly Regex NumeroNrRegex = new(@"NR\s*-?\s*0*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static int? NumeroNr(string? norma)
    {
        var m = norma is null ? null : NumeroNrRegex.Match(norma);
        return m is { Success: true } ? int.Parse(m.Groups[1].Value) : null;
    }

    public static async Task<ResultadoGeracaoCursosObrigatoriosDto> GerarAsync(IAppDbContext db, Guid obraId, CancellationToken ct)
    {
        // Consultas por tabela e junção em memória: projetar por navegação obrigatória esconde a
        // linha quando o pai está excluído (soft delete) — o erro que já derrubou a entrega de EPI.
        var ghes = await db.Ghes.Where(g => g.ObraId == obraId).Select(g => new { g.Id, g.AtividadesCriticas }).ToListAsync(ct);
        var idsGhes = ghes.Select(g => g.Id).ToList();
        var gheFuncoes = await db.GheFuncoes.Where(f => idsGhes.Contains(f.GheId))
            .Select(f => new { f.GheId, f.FuncaoId, f.DescricaoAtividades }).ToListAsync(ct);
        var atividades = await db.Atividades.Where(a => a.ObraId == obraId)
            .Select(a => new { a.Id, a.GheId, a.Nome, a.Descricao, a.CondicoesExecucao, a.Ferramentas }).ToListAsync(ct);
        var idsAtividades = atividades.Select(a => a.Id).ToList();
        var riscos = await db.Riscos.Where(r => idsAtividades.Contains(r.AtividadeId))
            .Select(r => new { r.AtividadeId, r.PerigoId }).ToListAsync(ct);
        var idsPerigos = riscos.Select(r => r.PerigoId).Distinct().ToList();
        var perigos = await db.Perigos.Where(p => idsPerigos.Contains(p.Id))
            .Select(p => new { p.Id, p.Nome, p.Agente, p.Fonte }).ToDictionaryAsync(p => p.Id, ct);

        var regras = await db.RegrasCursoObrigatorio.ToListAsync(ct);
        var cursos = await db.CursosTreinamento.Select(c => new { c.Id, c.Nome, c.NormaReferencia }).ToListAsync(ct);
        var pgrId = await db.Pgrs.Where(p => p.ObraId == obraId && p.Status == StatusPgr.Vigente)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);

        // Contando os desativados: um registro que saiu de uso volta como sugestão em vez de
        // violar o índice único (ObraId, FuncaoId, CursoTreinamentoId).
        var existentes = (await db.CursosObrigatoriosFuncaoObra.IgnoreQueryFilters()
                .Where(c => c.ObraId == obraId).ToListAsync(ct))
            .ToDictionary(c => (c.FuncaoId, c.CursoTreinamentoId));

        var normasSemCurso = new SortedSet<string>();
        var sugeridos = 0;

        foreach (var regra in regras)
        {
            var cursoId = regra.CursoTreinamentoId is { } id && cursos.Any(c => c.Id == id)
                ? id
                : cursos.Where(c => NumeroNr(c.NormaReferencia) is { } n && n == NumeroNr(regra.Norma))
                    .OrderBy(c => c.Nome).Select(c => (Guid?)c.Id).FirstOrDefault();

            var palavras = (regra.PalavrasChave ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(FuncaoSstClassifier.Normalizar).Where(p => p.Length > 0).ToList();

            foreach (var gf in gheFuncoes)
            {
                var motivo = regra.AplicaTodasFuncoes
                    ? "Obrigatório para todas as funções de canteiro"
                    : Procurar(palavras, gf.DescricaoAtividades, ghes.First(g => g.Id == gf.GheId).AtividadesCriticas,
                        atividades.Where(a => a.GheId == gf.GheId).Select(a => (
                            a.Nome,
                            Texto: string.Join(' ', a.Nome, a.Descricao, a.CondicoesExecucao, a.Ferramentas),
                            Perigos: riscos.Where(r => r.AtividadeId == a.Id && perigos.ContainsKey(r.PerigoId))
                                .Select(r => perigos[r.PerigoId])
                                .Select(p => (p.Nome, Texto: string.Join(' ', p.Nome, p.Agente, p.Fonte))).ToList())));
                if (motivo is null) continue;

                if (cursoId is not { } curso)
                {
                    normasSemCurso.Add(regra.Norma);
                    continue;
                }

                if (existentes.TryGetValue((gf.FuncaoId, curso), out var existente))
                {
                    if (existente.Ativo) continue;
                    existente.Ativo = true;
                    existente.Status = StatusCursoObrigatorio.Sugerido;
                    existente.OrigemSugestao = OrigemCursoObrigatorio.Pgr;
                    existente.Motivo = Limitar(motivo);
                    existente.PgrId = pgrId;
                    existente.JustificativaRecusa = null;
                    existente.DecididoPorUsuarioId = null;
                    existente.DecididoPorNome = null;
                    existente.DecididoEmUtc = null;
                    sugeridos++;
                    continue;
                }

                var novo = new CursoObrigatorioFuncaoObra
                {
                    ObraId = obraId,
                    FuncaoId = gf.FuncaoId,
                    CursoTreinamentoId = curso,
                    Status = StatusCursoObrigatorio.Sugerido,
                    OrigemSugestao = OrigemCursoObrigatorio.Pgr,
                    Motivo = Limitar(motivo),
                    PgrId = pgrId,
                };
                db.CursosObrigatoriosFuncaoObra.Add(novo);
                existentes[(gf.FuncaoId, curso)] = novo;
                sugeridos++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new ResultadoGeracaoCursosObrigatoriosDto(sugeridos, normasSemCurso.ToList());
    }

    // Primeiro achado vence, do mais forte para o mais fraco: perigo do PGR, texto da atividade,
    // descrição da função no GHE, atividades críticas do GHE.
    private static string? Procurar(
        List<string> palavras,
        string? descricaoFuncao,
        string? atividadesCriticas,
        IEnumerable<(string Nome, string Texto, List<(string Nome, string Texto)> Perigos)> atividades)
    {
        if (palavras.Count == 0) return null;
        string? Achar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var t = FuncaoSstClassifier.Normalizar(texto);
            return palavras.FirstOrDefault(p => t.Contains(p, StringComparison.Ordinal));
        }

        var lista = atividades.ToList();
        foreach (var a in lista)
            foreach (var p in a.Perigos)
                if (Achar(p.Texto) is not null)
                    return $"Perigo \"{p.Nome}\" na atividade {a.Nome}";
        foreach (var a in lista)
            if (Achar(a.Texto) is { } palavra)
                return $"Atividade {a.Nome} (\"{palavra}\")";
        if (Achar(descricaoFuncao) is { } naFuncao)
            return $"Descrição da função no GHE (\"{naFuncao}\")";
        if (Achar(atividadesCriticas) is { } critica)
            return $"Atividade crítica do GHE (\"{critica}\")";
        return null;
    }

    private static string Limitar(string s) => s.Length <= 500 ? s : s[..499] + "…";
}
