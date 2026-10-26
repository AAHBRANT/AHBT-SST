using AAHBRANT.SST.Application.Asos;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

/// <summary>
/// Quantos trabalhadores ativos podem trabalhar hoje e por que os demais estão bloqueados. Alimenta o
/// indicador "Podem trabalhar hoje" do Início. É a mesma regra de CalculadoraLiberacaoTerceirizado, só que
/// em lote (uma consulta por tabela, em vez de uma por trabalhador): ASO válido e treinamentos obrigatórios da
/// função em dia. EPI não bloqueia (decisão do usuário, 05/10): nenhum EPI é obrigatório para liberar, e a matriz
/// de EPI da função lista alternativas (ex.: protetor auditivo concha ou plug), não um kit exigido por inteiro.
/// </summary>
public record ObterLiberacaoParaTrabalhoQuery(Guid? ObraId = null) : IRequest<LiberacaoTrabalhoDto>;

/// <param name="Ativos">Trabalhadores ativos considerados.</param>
/// <param name="Liberados">Sem nenhuma pendência.</param>
/// <param name="SemAsoValido">Sem ASO apto (ou apto com restrição) dentro da validade.</param>
/// <param name="TreinamentoPendente">Com algum treinamento obrigatório vencido ou nunca feito.</param>
/// <param name="AsoSemResultado">Dos sem ASO válido: tem ASO na validade com resultado Pendente (aptidão não marcada no G-RH).</param>
/// <param name="AsoInapto">Dos sem ASO válido: tem ASO na validade com resultado Inapto.</param>
/// <param name="AsoVencido">Dos sem ASO válido: só tem ASO com validade passada.</param>
/// <param name="SemAso">Dos sem ASO válido: nenhum ASO cadastrado.</param>
/// <remarks>Uma pessoa pode ter mais de um motivo, então os motivos não somam o total de bloqueados. Já os 4 motivos
/// do ASO são exclusivos e somam SemAsoValido.</remarks>
public record LiberacaoTrabalhoDto(int Ativos, int Liberados, int SemAsoValido, int TreinamentoPendente,
    int AsoSemResultado = 0, int AsoInapto = 0, int AsoVencido = 0, int SemAso = 0)
{
    public int Bloqueados => Ativos - Liberados;
}

public class ObterLiberacaoParaTrabalhoQueryHandler : IRequestHandler<ObterLiberacaoParaTrabalhoQuery, LiberacaoTrabalhoDto>
{
    private readonly IAppDbContext _db;
    public ObterLiberacaoParaTrabalhoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<LiberacaoTrabalhoDto> Handle(ObterLiberacaoParaTrabalhoQuery request, CancellationToken ct)
    {
        var hoje = DateTime.UtcNow.Date;

        // Consultas simples sobre tabelas isoladas e junção em memória, de propósito: projetar pela
        // navegação obrigatória faria o INNER JOIN esconder a linha quando o pai está excluído (soft delete).
        var trabalhadoresQuery = _db.Trabalhadores.Where(t => t.DataDemissao == null);
        if (request.ObraId is { } obraId) trabalhadoresQuery = trabalhadoresQuery.Where(t => t.ObraId == obraId);
        var trabalhadores = await trabalhadoresQuery.Select(t => new { t.Id, t.FuncaoId, t.Vinculo }).ToListAsync(ct);
        var ids = trabalhadores.Select(t => t.Id).ToList();

        var funcoesIsentas = (await _db.Funcoes.Select(f => new { f.Id, f.Nome }).ToListAsync(ct))
            .Where(f => FuncaoSstClassifier.EhTecnicoSeguranca(f.Nome))
            .Select(f => f.Id)
            .ToHashSet();

        // ASO vigente de cada pessoa (regra única, ver AsoVigente): o exame mais recente decide.
        var vigentes = AsoVigente.PorTrabalhador((await _db.Asos
                .Where(a => ids.Contains(a.TrabalhadorId))
                .Select(a => new AsoResumo(a.Id, a.TrabalhadorId, a.DataExame, a.DataValidade, a.ResultadoStatus))
                .ToListAsync(ct)));

        var cursosExistentes = (await _db.CursosTreinamento.Select(c => new { c.Id, c.EhIntegracaoSeguranca }).ToListAsync(ct))
            .ToDictionary(c => c.Id);
        var cursoIntegracaoId = cursosExistentes.Values.FirstOrDefault(c => c.EhIntegracaoSeguranca)?.Id;

        var cursosPorFuncao = (await _db.MatrizTreinamentoFuncoes
                .Select(m => new { m.FuncaoId, m.CursoTreinamentoId })
                .ToListAsync(ct))
            .Where(m => cursosExistentes.ContainsKey(m.CursoTreinamentoId))
            .GroupBy(m => m.FuncaoId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.CursoTreinamentoId).Distinct().ToList());

        var treinamentosValidos = (await _db.Treinamentos
                .Where(t => ids.Contains(t.TrabalhadorId))
                .Select(t => new { t.TrabalhadorId, t.CursoTreinamentoId, t.DataValidade })
                .ToListAsync(ct))
            .Where(t => t.DataValidade.Date >= hoje)
            .Select(t => (t.TrabalhadorId, t.CursoTreinamentoId))
            .ToHashSet();

        var liberados = 0;
        var semAso = 0;
        var treinamentoPendente = 0;
        int asoSemResultado = 0, asoInapto = 0, asoVencido = 0, semAsoNenhum = 0;

        foreach (var trabalhador in trabalhadores)
        {
            var situacaoAso = AsoVigente.Classificar(vigentes.TryGetValue(trabalhador.Id, out var vigente) ? vigente : null, hoje);
            var asoOk = situacaoAso == SituacaoAso.Liberado;

            var treinamentosOk = true;
            if (!funcoesIsentas.Contains(trabalhador.FuncaoId))
            {
                var exigidos = cursosPorFuncao.TryGetValue(trabalhador.FuncaoId, out var lista)
                    ? new List<Guid>(lista)
                    : new List<Guid>();
                // A Integração de Segurança é obrigatória só para terceirizado (regra do módulo Terceirizado).
                if (trabalhador.Vinculo == TipoVinculo.Terceirizado && cursoIntegracaoId is { } integracao && !exigidos.Contains(integracao))
                    exigidos.Add(integracao);
                treinamentosOk = exigidos.All(curso => treinamentosValidos.Contains((trabalhador.Id, curso)));
            }

            if (!asoOk)
            {
                semAso++;
                // Uma categoria por pessoa, pelo ASO vigente.
                switch (situacaoAso)
                {
                    case SituacaoAso.SemParecer: asoSemResultado++; break;
                    case SituacaoAso.Inapto: asoInapto++; break;
                    case SituacaoAso.Vencido: asoVencido++; break;
                    default: semAsoNenhum++; break;
                }
            }
            if (!treinamentosOk) treinamentoPendente++;
            if (asoOk && treinamentosOk) liberados++;
        }

        return new LiberacaoTrabalhoDto(trabalhadores.Count, liberados, semAso, treinamentoPendente,
            asoSemResultado, asoInapto, asoVencido, semAsoNenhum);
    }
}
