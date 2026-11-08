using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosObrigatorios;

// "Simular" da tela Regras por NR (07/11/2026): roda a regra (salva ou ainda em edição) contra o PGR
// de todas as obras e mostra quais funções receberiam o curso e por quê. Nada é gravado — serve para o
// QSMS validar a regra antes de salvar.
public record SimularRegraCursoObrigatorioQuery(
    string Norma,
    Guid? CursoTreinamentoId,
    bool AplicaTodasFuncoes,
    string? PalavrasChave,
    string? Perigos) : IRequest<SimulacaoRegraDto>;

public record SimulacaoRegraItemDto(string ObraNome, string FuncaoNome, string Motivo);

public record SimulacaoRegraDto(string? CursoNome, int ObrasAnalisadas, int FuncoesAnalisadas, List<SimulacaoRegraItemDto> Itens);

public class SimularRegraCursoObrigatorioQueryHandler : IRequestHandler<SimularRegraCursoObrigatorioQuery, SimulacaoRegraDto>
{
    private readonly IAppDbContext _db;
    public SimularRegraCursoObrigatorioQueryHandler(IAppDbContext db) => _db = db;

    public async Task<SimulacaoRegraDto> Handle(SimularRegraCursoObrigatorioQuery request, CancellationToken ct)
    {
        var cursos = await _db.CursosTreinamento.Select(c => new { c.Id, c.Nome, c.NormaReferencia }).ToListAsync(ct);
        var curso = cursos.FirstOrDefault(c => c.Id == request.CursoTreinamentoId)
            ?? cursos.Where(c => GeradorCursosObrigatorios.NumeroNr(c.NormaReferencia) is { } n && n == GeradorCursosObrigatorios.NumeroNr(request.Norma))
                .OrderBy(c => c.Nome).FirstOrDefault();

        var criterio = CriterioRegra.De(request.AplicaTodasFuncoes, request.PalavrasChave, request.Perigos);

        // Só obras com GHE (PGR estruturado); as encerradas ficam de fora.
        var obrasComGhe = await _db.Ghes.Select(g => g.ObraId).Distinct().ToListAsync(ct);
        var obras = await _db.Obras.Where(o => obrasComGhe.Contains(o.Id) && o.Status != StatusObra.Concluida && o.Status != StatusObra.Encerrada)
            .Select(o => new { o.Id, o.Nome }).ToListAsync(ct);
        var funcoes = await _db.Funcoes.Select(f => new { f.Id, f.Nome }).ToDictionaryAsync(f => f.Id, f => f.Nome, ct);

        var itens = new List<SimulacaoRegraItemDto>();
        var funcoesAnalisadas = 0;
        foreach (var obra in obras.OrderBy(o => o.Nome))
        {
            var dados = await GeradorCursosObrigatorios.CarregarAsync(_db, obra.Id, ct);
            var vistas = new HashSet<Guid>();
            foreach (var gf in dados.GheFuncoes)
            {
                funcoesAnalisadas++;
                if (!vistas.Add(gf.FuncaoId)) continue;
                if (GeradorCursosObrigatorios.Motivo(criterio, dados, gf) is { } motivo)
                    itens.Add(new SimulacaoRegraItemDto(obra.Nome, funcoes.GetValueOrDefault(gf.FuncaoId, "Função"), motivo));
            }
        }

        return new SimulacaoRegraDto(curso?.Nome, obras.Count, funcoesAnalisadas, itens);
    }
}

// Nomes padronizados de perigo que o PGR gerado pela EAP usa — a tela oferece estes para a regra.
public record ListarPerigosPadraoQuery : IRequest<List<string>>;

public class ListarPerigosPadraoQueryHandler : IRequestHandler<ListarPerigosPadraoQuery, List<string>>
{
    public Task<List<string>> Handle(ListarPerigosPadraoQuery request, CancellationToken ct)
        => Task.FromResult(PerigosPadrao.Catalogo.Select(p => p.Nome).ToList());
}
