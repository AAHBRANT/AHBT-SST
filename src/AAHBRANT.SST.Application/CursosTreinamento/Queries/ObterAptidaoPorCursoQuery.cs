using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosTreinamento.Queries;

/// <summary>
/// Para cada curso exigido por alguma função (MatrizTreinamentoFuncao), quantos trabalhadores ativos
/// precisam dele e em que situação estão. Alimenta o card "Aptidão por treinamento" do Início.
/// "Apto" é só o treinamento do curso válido hoje — o ASO e o EPI não entram (decisão do usuário, 04/10).
/// </summary>
public record ObterAptidaoPorCursoQuery(Guid? ObraId = null) : IRequest<List<AptidaoCursoDto>>;

/// <param name="EmDia">Treinamento válido por mais de 30 dias.</param>
/// <param name="VencemEm30Dias">Ainda válido hoje, mas vence em até 30 dias (também é apto).</param>
/// <param name="Vencidos">O treinamento mais recente do curso já venceu.</param>
/// <param name="SemCurso">Nunca registrou o curso.</param>
public record AptidaoCursoDto(
    Guid CursoId,
    string Nome,
    string? NormaReferencia,
    int Exigidos,
    int EmDia,
    int VencemEm30Dias,
    int Vencidos,
    int SemCurso);

public class ObterAptidaoPorCursoQueryHandler : IRequestHandler<ObterAptidaoPorCursoQuery, List<AptidaoCursoDto>>
{
    // Mesmo limiar de "a vencer" usado no restante do sistema (TreinamentosTab e KPIs do Início).
    private const int DiasParaVencer = 30;

    private readonly IAppDbContext _db;
    public ObterAptidaoPorCursoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AptidaoCursoDto>> Handle(ObterAptidaoPorCursoQuery request, CancellationToken ct)
    {
        var hoje = DateTime.UtcNow.Date;

        // Consultas simples sobre tabelas isoladas e junção em memória, de propósito: projetar pela
        // navegação obrigatória faria o INNER JOIN esconder a linha quando o pai está excluído
        // (soft delete), que é exatamente o erro que já derrubou a entrega de EPI.
        var trabalhadoresQuery = _db.Trabalhadores.Where(t => t.DataDemissao == null);
        if (request.ObraId is { } obraId) trabalhadoresQuery = trabalhadoresQuery.Where(t => t.ObraId == obraId);
        var trabalhadores = await trabalhadoresQuery.Select(t => new { t.Id, t.FuncaoId }).ToListAsync(ct);

        var funcoesIsentas = (await _db.Funcoes.Select(f => new { f.Id, f.Nome }).ToListAsync(ct))
            .Where(f => FuncaoSstClassifier.EhTecnicoSeguranca(f.Nome))
            .Select(f => f.Id)
            .ToHashSet();

        var cursos = await _db.CursosTreinamento
            .Select(c => new { c.Id, c.Nome, c.NormaReferencia })
            .ToListAsync(ct);
        var cursoPorId = cursos.ToDictionary(c => c.Id);

        var exigenciasPorFuncao = (await _db.MatrizTreinamentoFuncoes
                .Select(m => new { m.FuncaoId, m.CursoTreinamentoId })
                .ToListAsync(ct))
            .Where(m => cursoPorId.ContainsKey(m.CursoTreinamentoId))
            .GroupBy(m => m.FuncaoId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.CursoTreinamentoId).Distinct().ToList());

        var idsCursosExigidos = exigenciasPorFuncao.Values.SelectMany(c => c).Distinct().ToList();
        var idsTrabalhadores = trabalhadores.Select(t => t.Id).ToList();

        // Validade mais recente por (trabalhador, curso): renovar o curso não pode deixar o registro
        // antigo, já vencido, contando como pendência.
        var validadePorTrabalhadorCurso = (await _db.Treinamentos
                .Where(t => idsCursosExigidos.Contains(t.CursoTreinamentoId) && idsTrabalhadores.Contains(t.TrabalhadorId))
                .Select(t => new { t.TrabalhadorId, t.CursoTreinamentoId, t.DataValidade })
                .ToListAsync(ct))
            .GroupBy(t => (t.TrabalhadorId, t.CursoTreinamentoId))
            .ToDictionary(g => g.Key, g => g.Max(t => t.DataValidade.Date));

        var acumulado = new Dictionary<Guid, int[]>(); // [exigidos, emDia, vencem, vencidos, semCurso]
        foreach (var trabalhador in trabalhadores)
        {
            if (funcoesIsentas.Contains(trabalhador.FuncaoId)) continue;
            if (!exigenciasPorFuncao.TryGetValue(trabalhador.FuncaoId, out var exigidos)) continue;

            foreach (var cursoId in exigidos)
            {
                if (!acumulado.TryGetValue(cursoId, out var contagem)) acumulado[cursoId] = contagem = new int[5];
                contagem[0]++;

                if (!validadePorTrabalhadorCurso.TryGetValue((trabalhador.Id, cursoId), out var validade)) contagem[4]++;
                else if (validade < hoje) contagem[3]++;
                else if ((validade - hoje).TotalDays <= DiasParaVencer) contagem[2]++;
                else contagem[1]++;
            }
        }

        return acumulado
            .Select(par =>
            {
                var curso = cursoPorId[par.Key];
                return new AptidaoCursoDto(curso.Id, curso.Nome, curso.NormaReferencia,
                    par.Value[0], par.Value[1], par.Value[2], par.Value[3], par.Value[4]);
            })
            .OrderByDescending(c => c.Exigidos)
            .ThenBy(c => c.Nome)
            .ToList();
    }
}
