using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Cronograma de treinamentos do PGR (NR-01, item 1.7): cursos obrigatórios de cada função de cada GHE, tirados dos
// cursos obrigatórios da obra. Alimenta a seção 7.1 do PDF e o cartão da tela de aprovação.
internal static class CronogramaTreinamentosPgr
{
    // Sugeridos pelo PGR ou aprovados pelo engenheiro; Recusado não entra. Consultas por tabela e junção em memória,
    // para não perder linha de pai excluído (soft delete).
    public static async Task<List<LinhaTreinamentoPdf>> MontarAsync(
        IAppDbContext db, Guid obraId, IReadOnlyList<(int Ghe, string Nome, Guid Id)> funcoesDosGhes, CancellationToken ct)
    {
        if (funcoesDosGhes.Count == 0) return new();
        var obrigatorios = await db.CursosObrigatoriosFuncaoObra
            .Where(c => c.ObraId == obraId && c.Status != StatusCursoObrigatorio.Recusado)
            .Select(c => new { c.FuncaoId, c.CursoTreinamentoId, c.Status, c.Motivo }).ToListAsync(ct);
        if (obrigatorios.Count == 0) return new();
        var idsCursos = obrigatorios.Select(c => c.CursoTreinamentoId).Distinct().ToList();
        var cursos = await db.CursosTreinamento.Where(c => idsCursos.Contains(c.Id))
            .Select(c => new { c.Id, c.Nome, c.NormaReferencia, c.CargaHorariaMinima, c.ValidadeEmMeses }).ToDictionaryAsync(c => c.Id, ct);

        var linhas = new List<LinhaTreinamentoPdf>();
        foreach (var (ghe, nome, idFuncao) in funcoesDosGhes)
            foreach (var o in obrigatorios.Where(x => x.FuncaoId == idFuncao))
                if (cursos.TryGetValue(o.CursoTreinamentoId, out var curso))
                    linhas.Add(new LinhaTreinamentoPdf(ghe, nome, curso.Nome, curso.NormaReferencia, curso.CargaHorariaMinima,
                        curso.ValidadeEmMeses, o.Motivo, o.Status == StatusCursoObrigatorio.Aprovado));
        return linhas.OrderBy(l => l.GheNumero).ThenBy(l => l.Funcao, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(l => l.Norma ?? l.Curso, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}

public record GheCronogramaDto(int Numero, string Nome);

public record CronogramaTreinamentosDto(IReadOnlyList<GheCronogramaDto> Ghes, IReadOnlyList<LinhaTreinamentoPdf> Linhas);

// Cartão "Cronograma de treinamentos" da tela de aprovação. Só depois da aprovação, como o PDF.
public record ObterCronogramaTreinamentosGeracaoPgrQuery(Guid Id) : IRequest<CronogramaTreinamentosDto>;

public class ObterCronogramaTreinamentosGeracaoPgrQueryHandler : IRequestHandler<ObterCronogramaTreinamentosGeracaoPgrQuery, CronogramaTreinamentosDto>
{
    private readonly IAppDbContext _db;

    public ObterCronogramaTreinamentosGeracaoPgrQueryHandler(IAppDbContext db) => _db = db;

    public async Task<CronogramaTreinamentosDto> Handle(ObterCronogramaTreinamentosGeracaoPgrQuery request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        if (g.Status != StatusGeracaoPgrEap.Aprovado)
            throw new InvalidOperationException("O cronograma de treinamentos fica disponível depois da aprovação do Engenheiro de Segurança.");
        var estrutura = SerializacaoGeracao.Ler(g.EstruturaJson);

        var funcoesCadastro = await _db.Funcoes.Select(f => new { f.Id, f.Nome }).ToListAsync(ct);
        var porNome = new Dictionary<string, Guid>();
        foreach (var f in funcoesCadastro) porNome.TryAdd(FuncaoSstClassifier.Normalizar(f.Nome), f.Id);

        var funcoesDosGhes = new List<(int Ghe, string Nome, Guid Id)>();
        foreach (var ghe in estrutura.Ghes.OrderBy(x => x.Numero))
            foreach (var f in ghe.Funcoes)
            {
                Guid? id = f.FuncaoId;
                if (id is null && porNome.TryGetValue(FuncaoSstClassifier.Normalizar(f.Nome), out var achada)) id = achada;
                if (id is { } idFuncao) funcoesDosGhes.Add((ghe.Numero, f.Nome, idFuncao));
            }

        var linhas = await CronogramaTreinamentosPgr.MontarAsync(_db, g.ObraId, funcoesDosGhes, ct);
        var ghes = estrutura.Ghes.OrderBy(x => x.Numero).Select(x => new GheCronogramaDto(x.Numero, x.Nome)).ToList();
        return new CronogramaTreinamentosDto(ghes, linhas);
    }
}
