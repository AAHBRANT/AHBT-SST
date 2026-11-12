using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosObrigatorios;

// NR-01 = a própria Ordem de Serviço assinada (decisão de 11/11/2026, docs/2026-11-11-realizar-integracao.md). Um curso
// "NR-01 - ORDEM DE SERVIÇO" que o PGR aprova para a função não pode bloquear a emissão da OS (seria circular): ele não
// entra na minuta, não é agendado e não vira etapa de curso. Nos demais lugares (liberação, terceirizado, plano de
// integração) conta como cumprido quando o trabalhador tem a OS vigente da obra assinada por ele mesmo.
public static partial class CursoNr01OrdemServico
{
    public const string TextoNaoAssinada = "Ordem de Serviço (NR-01) não assinada";

    // NR-01, NR 01, NR-1, NR1 no começo do texto; o (?!\d) evita NR-10, NR-11, NR-12 etc.
    [GeneratedRegex(@"^\s*NR\s*[-.]?\s*0*1(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex ComecaComNr01();

    // Norma de referência NR-01 (qualquer grafia), nome começando com NR-01/NR 01 ou contendo "Ordem de Serviço".
    public static bool Eh(string? nome, string? normaReferencia)
    {
        if (!string.IsNullOrWhiteSpace(normaReferencia) && ComecaComNr01().IsMatch(SemAcento(normaReferencia)))
            return true;
        if (string.IsNullOrWhiteSpace(nome)) return false;
        var n = SemAcento(nome);
        return ComecaComNr01().IsMatch(n) || n.Contains("ORDEM DE SERVI", StringComparison.Ordinal);
    }

    public static bool Eh(CursoTreinamento curso) => Eh(curso.Nome, curso.NormaReferencia);

    // Trabalhadores (dos pares trabalhador/obra informados) com a OS vigente da obra assinada pelo funcionário (digital ou
    // facial). Só a revisão mais recente da obra vale. Sem o filtro por obra do usuário (é dado de cumprimento, e rota
    // anônima não pode depender dele); mantém só o filtro de ativo.
    public static async Task<HashSet<Guid>> TrabalhadoresComOsAssinadaAsync(
        IAppDbContext db, IEnumerable<(Guid TrabalhadorId, Guid ObraId)> pessoas, CancellationToken ct)
    {
        var lista = pessoas.Distinct().ToList();
        var resultado = new HashSet<Guid>();
        if (lista.Count == 0) return resultado;
        var ids = lista.Select(p => p.TrabalhadorId).Distinct().ToList();
        var ordens = await db.OrdensServico.IgnoreQueryFilters()
            .Where(o => o.Ativo && ids.Contains(o.TrabalhadorId))
            .ToListAsync(ct);
        var vigentes = lista
            .Select(p => ordens.Where(o => o.TrabalhadorId == p.TrabalhadorId && o.ObraId == p.ObraId)
                .OrderByDescending(o => o.Revisao).FirstOrDefault())
            .Where(o => o is not null).Select(o => o!).ToList();
        if (vigentes.Count == 0) return resultado;
        var assinaturas = await AssinaturasOrdemServico.CarregarAsync(db, vigentes, ct);
        foreach (var os in vigentes)
            if (assinaturas.TryGetValue(os.Id, out var a) && a.Funcionario is not null)
                resultado.Add(os.TrabalhadorId);
        return resultado;
    }

    private static string SemAcento(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().ToUpperInvariant();
    }
}
