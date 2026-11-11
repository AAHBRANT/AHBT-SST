using AAHBRANT.SST.Application.CursosObrigatorios;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Integracoes;
using AAHBRANT.SST.Application.SessoesTreinamento;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

// Carrega do banco o que a calculadora precisa (treinamentos válidos e cursos exigidos pelo PGR) e o catálogo dos
// quatro cursos das etapas. Consultas simples por tabela (sem navegação obrigatória).
public static class ExigenciasEtapasIntegracao
{
    public static DateTime HojeBrasilia() => AAHBRANT.SST.Application.PlanosIntegracao.AgendaTreinamentosIntegracao.HojeBrasilia();

    // Curso de cada etapa de curso, pelo catálogo (a norma do curso; NR-06 também pelo marcador AtendeNr6).
    // Se houver mais de um curso da mesma norma, vale o de nome mais curto/primeiro por nome (determinístico).
    public static async Task<IReadOnlyDictionary<TipoEtapaIntegracao, CursoEtapaIntegracao>> CarregarCursosAsync(
        IAppDbContext db, string? obraNome, CancellationToken ct)
    {
        var cursos = await db.CursosTreinamento.OrderBy(c => c.Nome).ToListAsync(ct);
        var resultado = new Dictionary<TipoEtapaIntegracao, CursoEtapaIntegracao>();
        foreach (var d in CalculadoraEtapasIntegracao.Definicao.Where(x => x.Norma is not null))
        {
            var curso = cursos.FirstOrDefault(c => PacoteIntegracao.NormaDoCurso(c) == d.Norma);
            if (curso is null) continue;
            var padrao = PadraoTurmaTreinamento.De(curso, obraNome);
            resultado[d.Tipo] = new CursoEtapaIntegracao(curso.Id, padrao.CargaHorariaMinima);
        }
        return resultado;
    }

    public static async Task<List<PessoaEtapasIntegracao>> CarregarPessoasAsync(
        IAppDbContext db, IReadOnlyCollection<Trabalhador> trabalhadores, Guid obraId, CancellationToken ct)
    {
        var hoje = HojeBrasilia();
        var ids = trabalhadores.Select(t => t.Id).ToList();
        var cursos = await db.CursosTreinamento.ToListAsync(ct);
        var normaPorCurso = cursos
            .Select(c => (c.Id, Norma: PacoteIntegracao.NormaDoCurso(c)))
            .Where(x => x.Norma is not null)
            .ToDictionary(x => x.Id, x => x.Norma!);

        var validos = await db.Treinamentos
            .Where(t => ids.Contains(t.TrabalhadorId) && t.DataValidade.Date >= hoje)
            .Select(t => new { t.TrabalhadorId, t.CursoTreinamentoId })
            .ToListAsync(ct);
        var funcoes = await db.Funcoes.Select(f => new { f.Id, f.Nome }).ToListAsync(ct);
        var nomeFuncao = funcoes.ToDictionary(f => f.Id, f => f.Nome);

        var exigencias = await ExigenciasTreinamento.CarregarAsync(db, [obraId], ct);

        var pessoas = new List<PessoaEtapasIntegracao>();
        foreach (var t in trabalhadores)
        {
            var normasValidas = validos.Where(v => v.TrabalhadorId == t.Id && normaPorCurso.ContainsKey(v.CursoTreinamentoId))
                .Select(v => normaPorCurso[v.CursoTreinamentoId]).ToHashSet();
            var exigidos = exigencias(obraId, t.FuncaoId) ?? new List<Guid>();
            var normasExigidas = exigidos.Where(normaPorCurso.ContainsKey).Select(id => normaPorCurso[id]).ToHashSet();
            pessoas.Add(new PessoaEtapasIntegracao(
                t.Id, FuncaoSstClassifier.EhTecnicoSeguranca(nomeFuncao.GetValueOrDefault(t.FuncaoId)), normasValidas, normasExigidas));
        }
        return pessoas;
    }
}
