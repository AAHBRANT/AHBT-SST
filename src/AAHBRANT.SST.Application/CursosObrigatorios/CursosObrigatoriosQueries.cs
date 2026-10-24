using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosObrigatorios;

public record CursoObrigatorioDto(
    Guid Id,
    Guid FuncaoId,
    string FuncaoNome,
    int? GheNumero,
    string? GheSetor,
    int Trabalhadores,
    Guid CursoTreinamentoId,
    string CursoNome,
    string? NormaReferencia,
    StatusCursoObrigatorio Status,
    OrigemCursoObrigatorio Origem,
    string? Motivo,
    string? JustificativaRecusa,
    string? DecididoPorNome,
    DateTime? DecididoEmUtc);

public record CursosObrigatoriosObraDto(string? PgrNome, DateTime? PgrDataElaboracao, int FuncoesNoPgr, List<CursoObrigatorioDto> Itens);

public record ListarCursosObrigatoriosObraQuery(Guid ObraId) : IRequest<CursosObrigatoriosObraDto>;

public class ListarCursosObrigatoriosObraQueryHandler : IRequestHandler<ListarCursosObrigatoriosObraQuery, CursosObrigatoriosObraDto>
{
    private readonly IAppDbContext _db;
    public ListarCursosObrigatoriosObraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<CursosObrigatoriosObraDto> Handle(ListarCursosObrigatoriosObraQuery request, CancellationToken ct)
    {
        // Tabelas isoladas e junção em memória (soft delete + INNER JOIN esconde linha).
        var itens = await _db.CursosObrigatoriosFuncaoObra.Where(c => c.ObraId == request.ObraId).ToListAsync(ct);
        var ghes = await _db.Ghes.Where(g => g.ObraId == request.ObraId).Select(g => new { g.Id, g.Numero, g.Setor }).ToListAsync(ct);
        var idsGhes = ghes.Select(g => g.Id).ToList();
        var gheFuncoes = await _db.GheFuncoes.Where(f => idsGhes.Contains(f.GheId)).Select(f => new { f.GheId, f.FuncaoId }).ToListAsync(ct);
        var idsFuncoes = itens.Select(i => i.FuncaoId).Concat(gheFuncoes.Select(f => f.FuncaoId)).Distinct().ToList();
        var funcoes = await _db.Funcoes.IgnoreQueryFilters().Where(f => idsFuncoes.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Nome, ct);
        var idsCursos = itens.Select(i => i.CursoTreinamentoId).Distinct().ToList();
        var cursos = await _db.CursosTreinamento.IgnoreQueryFilters().Where(c => idsCursos.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.Nome, c.NormaReferencia }, ct);
        var trabalhadoresPorFuncao = (await _db.Trabalhadores
                .Where(t => t.ObraId == request.ObraId && t.DataDemissao == null).Select(t => t.FuncaoId).ToListAsync(ct))
            .GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        var pgr = await _db.Pgrs.Where(p => p.ObraId == request.ObraId && p.Status == StatusPgr.Vigente)
            .Select(p => new { p.Nome, p.DataElaboracao }).FirstOrDefaultAsync(ct);

        var lista = itens.Select(i =>
            {
                var ghe = gheFuncoes.Where(f => f.FuncaoId == i.FuncaoId)
                    .Select(f => ghes.First(g => g.Id == f.GheId)).OrderBy(g => g.Numero).FirstOrDefault();
                var curso = cursos.GetValueOrDefault(i.CursoTreinamentoId);
                return new CursoObrigatorioDto(i.Id, i.FuncaoId, funcoes.GetValueOrDefault(i.FuncaoId) ?? "(função excluída)",
                    ghe?.Numero, ghe?.Setor, trabalhadoresPorFuncao.GetValueOrDefault(i.FuncaoId),
                    i.CursoTreinamentoId, curso?.Nome ?? "(curso excluído)", curso?.NormaReferencia,
                    i.Status, i.OrigemSugestao, i.Motivo, i.JustificativaRecusa, i.DecididoPorNome, i.DecididoEmUtc);
            })
            .OrderBy(d => d.GheNumero ?? int.MaxValue).ThenBy(d => d.FuncaoNome).ThenBy(d => d.NormaReferencia).ThenBy(d => d.CursoNome)
            .ToList();

        return new CursosObrigatoriosObraDto(pgr?.Nome, pgr?.DataElaboracao, gheFuncoes.Select(f => f.FuncaoId).Distinct().Count(), lista);
    }
}

public record RegraCursoObrigatorioDto(
    Guid Id,
    string Norma,
    Guid? CursoTreinamentoId,
    string? CursoNome,
    string Descricao,
    bool AplicaTodasFuncoes,
    string? PalavrasChave,
    bool Ativo);

// Inclui as desativadas: a tela mostra e permite reativar.
public record ListarRegrasCursoObrigatorioQuery : IRequest<List<RegraCursoObrigatorioDto>>;

public class ListarRegrasCursoObrigatorioQueryHandler : IRequestHandler<ListarRegrasCursoObrigatorioQuery, List<RegraCursoObrigatorioDto>>
{
    private readonly IAppDbContext _db;
    public ListarRegrasCursoObrigatorioQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<RegraCursoObrigatorioDto>> Handle(ListarRegrasCursoObrigatorioQuery request, CancellationToken ct)
    {
        var regras = await _db.RegrasCursoObrigatorio.IgnoreQueryFilters().ToListAsync(ct);
        var cursos = await _db.CursosTreinamento.Select(c => new { c.Id, c.Nome, c.NormaReferencia }).ToListAsync(ct);
        return regras
            .Select(r =>
            {
                // Mesmo critério da geração: o curso fixado, senão o primeiro do catálogo daquela NR.
                var curso = cursos.FirstOrDefault(c => c.Id == r.CursoTreinamentoId)
                    ?? cursos.Where(c => GeradorCursosObrigatorios.NumeroNr(c.NormaReferencia) is { } n && n == GeradorCursosObrigatorios.NumeroNr(r.Norma))
                        .OrderBy(c => c.Nome).FirstOrDefault();
                return new RegraCursoObrigatorioDto(r.Id, r.Norma, r.CursoTreinamentoId, curso?.Nome, r.Descricao,
                    r.AplicaTodasFuncoes, r.PalavrasChave, r.Ativo);
            })
            .OrderByDescending(r => r.Ativo).ThenByDescending(r => r.AplicaTodasFuncoes)
            .ThenBy(r => GeradorCursosObrigatorios.NumeroNr(r.Norma)).ThenBy(r => r.Descricao)
            .ToList();
    }
}
