using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.CursosObrigatorios;
using AAHBRANT.SST.Application.MatrizRisco;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Resumo por função do PGR (mockup aprovado em 23/11/2026): o PGR é organizado por GHE, mas quem confere e quem usa
// pensa por função. Para cada função junta os riscos de todos os GHEs em que ela está, os EPIs das medidas e os
// treinamentos que as regras Risco → NR pedem. Só leitura: nada é gravado. Serve tanto para o rascunho (conferir antes
// de enviar para aprovação) quanto para o PGR aprovado (a geração que o originou).

public record GheDaFuncaoDto(int Numero, string Nome);

/// <param name="GhesSemMedida">GHEs em que este perigo aparece sem nenhuma medida de prevenção.</param>
public record RiscoDaFuncaoDto(
    string Perigo, string Tipo, int Probabilidade, int Severidade, NivelRisco? Nivel,
    int Medidas, List<int> GhesSemMedida, bool IncluidoPeloTecnico);

public record EpiDaFuncaoDto(string Nome, string Especificacao, List<string> Perigos);

public record TreinamentoDaFuncaoDto(string Norma, string Descricao, List<string> Motivos);

public record ResumoFuncaoPgrDto(
    string Nome, string? Cbo, string? CboTitulo, List<GheDaFuncaoDto> Ghes,
    List<RiscoDaFuncaoDto> Riscos, List<EpiDaFuncaoDto> Epis, List<TreinamentoDaFuncaoDto> Treinamentos)
{
    public bool TemQuedaDeAltura => Riscos.Any(r => ResumoPorFuncaoGeracaoPgr.EhQuedaDeAltura(r.Perigo));
    public int RiscosSemMedida => Riscos.Count(r => r.GhesSemMedida.Count > 0);
}

public record ResumoPorFuncaoGeracaoPgrDto(Guid GeracaoId, int TotalGhes, List<ResumoFuncaoPgrDto> Funcoes);

// Regra Risco → NR já lida do cadastro (Treinamentos › Regras por NR).
public record RegraTreinamentoResumo(string Norma, string Descricao, bool TodasAsFuncoes, string? PalavrasChave, string? Perigos);

public static class ResumoPorFuncaoGeracaoPgr
{
    public const string MotivoTodas = "Todas as funções do canteiro";

    public static bool EhQuedaDeAltura(string perigo) =>
        FuncaoSstClassifier.Normalizar(perigo).Contains("queda de altura", StringComparison.Ordinal);

    public static List<ResumoFuncaoPgrDto> Montar(
        EstruturaGeracaoPgr e,
        IReadOnlyList<(int P, int S, NivelRisco Nivel)> matriz,
        IReadOnlyList<RegraTreinamentoResumo> regras)
    {
        NivelRisco? Nivel(int p, int s) => matriz.Where(c => c.P == p && c.S == s).Select(c => (NivelRisco?)c.Nivel).FirstOrDefault();

        // A mesma NR pode estar cadastrada mais de uma vez: une os critérios por norma.
        var criterios = regras
            .GroupBy(r => GeradorCursosObrigatorios.NumeroNr(r.Norma)?.ToString() ?? r.Norma.Trim().ToUpperInvariant())
            .Select(g => (
                Norma: g.First().Norma.Trim(),
                Descricao: g.First().Descricao.Trim(),
                Criterio: CriterioRegra.De(
                    g.Any(r => r.TodasAsFuncoes),
                    string.Join(",", g.Select(r => r.PalavrasChave)),
                    string.Join(",", g.Select(r => r.Perigos)))))
            .OrderBy(r => GeradorCursosObrigatorios.NumeroNr(r.Norma) ?? int.MaxValue).ThenBy(r => r.Norma)
            .ToList();

        var porFuncao = e.Ghes
            .SelectMany(g => g.Funcoes.Where(f => !string.IsNullOrWhiteSpace(f.Nome)).Select(f => (Ghe: g, Funcao: f)))
            .GroupBy(x => FuncaoSstClassifier.Normalizar(x.Funcao.Nome));

        var resultado = new List<ResumoFuncaoPgrDto>();
        foreach (var grupo in porFuncao)
        {
            var primeira = grupo.First().Funcao;
            var ghes = grupo.Select(x => x.Ghe).DistinctBy(g => g.Numero).OrderBy(g => g.Numero).ToList();

            var riscos = new Dictionary<string, (RiscoGerado Pior, int Medidas, SortedSet<int> SemMedida, bool Manual)>();
            var epis = new Dictionary<(string Nome, string Especificacao), List<string>>();
            var motivos = criterios.ToDictionary(c => c.Norma, _ => new List<string>());

            void Motivo(string norma, string texto)
            {
                var lista = motivos[norma];
                if (!lista.Contains(texto, StringComparer.OrdinalIgnoreCase)) lista.Add(texto);
            }

            foreach (var ghe in ghes)
            {
                var descricaoFuncao = grupo.Where(x => x.Ghe.Numero == ghe.Numero).Select(x => x.Funcao.Descricao).FirstOrDefault();
                foreach (var atividade in ghe.Atividades)
                {
                    foreach (var r in atividade.Riscos.Where(r => !r.Excluido && !string.IsNullOrWhiteSpace(r.Perigo)))
                    {
                        var perigo = r.Perigo.Trim();
                        var chave = FuncaoSstClassifier.Normalizar(perigo);
                        var medidas = r.Medidas.Where(m => m.EntraNoPgr() && !string.IsNullOrWhiteSpace(m.Texto)).ToList();

                        if (!riscos.TryGetValue(chave, out var atual))
                            atual = (r, 0, new SortedSet<int>(), false);
                        var pior = Pontos(r, Nivel) > Pontos(atual.Pior, Nivel) ? r : atual.Pior;
                        if (medidas.Count == 0) atual.SemMedida.Add(ghe.Numero);
                        riscos[chave] = (pior, Math.Max(atual.Medidas, medidas.Count), atual.SemMedida, atual.Manual || r.IncluidoPeloTecnico);

                        foreach (var m in medidas.Where(m => m.CatalogoEpiId is not null || m.Categoria == CategoriasMedida.Epi))
                        {
                            var tipos = EpisDoPgr.Reconhecer(m.Texto);
                            var chaves = tipos.Count > 0
                                ? tipos.Select(t => (t.Nome, t.Especificacao))
                                : new[] { (m.Texto.Trim(), "Conferir o EPI e a especificação") };
                            foreach (var c in chaves)
                            {
                                if (!epis.TryGetValue(c, out var perigos)) epis[c] = perigos = new List<string>();
                                if (!perigos.Contains(perigo, StringComparer.OrdinalIgnoreCase)) perigos.Add(perigo);
                            }
                        }

                        // Mesmo texto que a geração dos cursos lê do PGR aprovado (nome do perigo + agente = tipo); os
                        // danos ficam de fora, senão "queda de materiais... da grua" pediria NR-11 e NR-35 para todos.
                        var textoRisco = FuncaoSstClassifier.Normalizar(string.Join(' ', perigo, r.Tipo));
                        foreach (var (norma, _, criterio) in criterios)
                        {
                            if (criterio.Todas) continue;
                            if (criterio.Perigos.Contains(chave) || criterio.Palavras.Any(p => textoRisco.Contains(p, StringComparison.Ordinal)))
                                Motivo(norma, perigo);
                        }
                    }

                    var textoAtividade = FuncaoSstClassifier.Normalizar(atividade.Nome);
                    foreach (var (norma, _, criterio) in criterios)
                        if (!criterio.Todas && criterio.Palavras.FirstOrDefault(p => textoAtividade.Contains(p, StringComparison.Ordinal)) is not null)
                            Motivo(norma, $"Atividade {atividade.CodigoEap} {atividade.Nome}".Trim());
                }

                var textoFuncao = FuncaoSstClassifier.Normalizar(descricaoFuncao ?? string.Empty);
                if (textoFuncao.Length > 0)
                    foreach (var (norma, _, criterio) in criterios)
                        if (!criterio.Todas && criterio.Palavras.Any(p => textoFuncao.Contains(p, StringComparison.Ordinal)))
                            Motivo(norma, "Descrição da função");
            }

            foreach (var (norma, _, criterio) in criterios)
                if (criterio.Todas) Motivo(norma, MotivoTodas);

            resultado.Add(new ResumoFuncaoPgrDto(
                primeira.Nome.Trim(),
                grupo.Select(x => x.Funcao.Cbo).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
                grupo.Select(x => x.Funcao.CboTitulo).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
                ghes.Select(g => new GheDaFuncaoDto(g.Numero, g.Nome)).ToList(),
                riscos.Values
                    .Select(x => new RiscoDaFuncaoDto(x.Pior.Perigo.Trim(), x.Pior.Tipo, x.Pior.Probabilidade, x.Pior.Severidade,
                        Nivel(x.Pior.Probabilidade, x.Pior.Severidade), x.Medidas, x.SemMedida.ToList(), x.Manual))
                    .OrderByDescending(x => (int?)x.Nivel ?? 0).ThenByDescending(x => x.Probabilidade * x.Severidade).ThenBy(x => x.Perigo)
                    .ToList(),
                epis.OrderBy(x => EpisDoPgr.Ordem(x.Key.Nome)).ThenBy(x => x.Key.Nome)
                    .Select(x => new EpiDaFuncaoDto(x.Key.Nome, x.Key.Especificacao, x.Value)).ToList(),
                criterios.Where(c => motivos[c.Norma].Count > 0)
                    .Select(c => new TreinamentoDaFuncaoDto(c.Norma, c.Descricao, motivos[c.Norma])).ToList()));
        }

        return resultado.OrderBy(f => f.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static int Pontos(RiscoGerado r, Func<int, int, NivelRisco?> nivel) =>
        ((int?)nivel(r.Probabilidade, r.Severidade) ?? 0) * 100 + r.Probabilidade * r.Severidade;
}

public record ObterResumoPorFuncaoGeracaoPgrQuery(Guid Id) : IRequest<ResumoPorFuncaoGeracaoPgrDto>;

public class ObterResumoPorFuncaoGeracaoPgrQueryHandler : IRequestHandler<ObterResumoPorFuncaoGeracaoPgrQuery, ResumoPorFuncaoGeracaoPgrDto>
{
    private readonly IAppDbContext _db;

    public ObterResumoPorFuncaoGeracaoPgrQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ResumoPorFuncaoGeracaoPgrDto> Handle(ObterResumoPorFuncaoGeracaoPgrQuery request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        var estrutura = SerializacaoGeracao.Ler(g.EstruturaJson);
        // Mesma unificação que a tela vê ao abrir a geração (Servente, Ajudante... viram Ajudante Geral).
        await AjudanteGeral.UnificarAsync(_db, estrutura, ct);

        var matriz = await _db.MatrizRiscoConfigs.Include(c => c.Celulas).VigenteMaisRecente().FirstOrDefaultAsync(ct);
        var celulas = matriz?.Celulas.Select(c => (c.Probabilidade, c.Severidade, c.NivelRisco)).ToList() ?? new();
        var regras = await _db.RegrasCursoObrigatorio.Where(r => r.Ativo)
            .Select(r => new RegraTreinamentoResumo(r.Norma, r.Descricao, r.AplicaTodasFuncoes, r.PalavrasChave, r.Perigos))
            .ToListAsync(ct);

        return new ResumoPorFuncaoGeracaoPgrDto(g.Id, estrutura.Ghes.Count, ResumoPorFuncaoGeracaoPgr.Montar(estrutura, celulas, regras));
    }
}
