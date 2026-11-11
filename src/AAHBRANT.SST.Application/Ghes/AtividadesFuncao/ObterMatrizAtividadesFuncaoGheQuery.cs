using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ghes.AtividadesFuncao;

// Estado de um par atividade × função. SemDecisao = ainda não existe registro.
public static class EstadoAtividadeFuncao
{
    public const string SemDecisao = "SemDecisao";
    public const string Pendente = "Pendente";
    public const string Validada = "Validada";
    public const string Descartada = "Descartada";

    public static string De(StatusMedidaPrevencao status) => status switch
    {
        StatusMedidaPrevencao.Validada => Validada,
        StatusMedidaPrevencao.Descartada => Descartada,
        _ => Pendente,
    };
}

public class FuncaoMatrizDto
{
    public Guid FuncaoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    // Descrição das atividades da função no PGR (GheFuncao.DescricaoAtividades).
    public string? DescricaoNoPgr { get; set; }
    public int Validadas { get; set; }
    public int Pendentes { get; set; }
    public int Descartadas { get; set; }
    public int SemDecisao { get; set; }
}

public class AtividadeMatrizDto
{
    public Guid AtividadeId { get; set; }
    public string Nome { get; set; } = string.Empty;
    // Procedimentos/ITs vigentes vinculados (zero = a OS bloqueia quando alguma função executa a atividade).
    public int Procedimentos { get; set; }
}

public class CelulaMatrizDto
{
    public Guid AtividadeId { get; set; }
    public Guid FuncaoId { get; set; }
    // "SemDecisao" | "Pendente" | "Validada" | "Descartada"
    public string Estado { get; set; } = EstadoAtividadeFuncao.SemDecisao;
    // "Pgr" | "SugestaoIa" | "Manual"; nulo quando SemDecisao.
    public string? Origem { get; set; }
    public string? JustificativaIa { get; set; }
    public string? ValidadoPorNome { get; set; }
    public DateTime? ValidadoEmUtc { get; set; }
}

public class MatrizAtividadesFuncaoDto
{
    public Guid ObraId { get; set; }
    public Guid GheId { get; set; }
    public int GheNumero { get; set; }
    public List<FuncaoMatrizDto> Funcoes { get; set; } = new();
    public List<AtividadeMatrizDto> Atividades { get; set; } = new();
    // Todos os pares (atividade × função), inclusive os sem decisão.
    public List<CelulaMatrizDto> Celulas { get; set; } = new();
}

public record ObterMatrizAtividadesFuncaoGheQuery(Guid ObraId, Guid GheId) : IRequest<MatrizAtividadesFuncaoDto?>;

public class ObterMatrizAtividadesFuncaoGheQueryHandler : IRequestHandler<ObterMatrizAtividadesFuncaoGheQuery, MatrizAtividadesFuncaoDto?>
{
    private readonly IAppDbContext _db;

    public ObterMatrizAtividadesFuncaoGheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<MatrizAtividadesFuncaoDto?> Handle(ObterMatrizAtividadesFuncaoGheQuery request, CancellationToken ct)
    {
        var ghe = await _db.Ghes.Where(g => g.Id == request.GheId && g.ObraId == request.ObraId)
            .Select(g => new { g.Id, g.Numero }).FirstOrDefaultAsync(ct);
        if (ghe is null) return null;

        // Consultas simples por tabela, sem navegação obrigatória (INNER JOIN + soft delete esconde registro).
        var atividades = await _db.Atividades.EmUso(_db)
            .Where(a => a.ObraId == request.ObraId && a.GheId == ghe.Id)
            .OrderBy(a => a.Nome).Select(a => new { a.Id, a.Nome }).ToListAsync(ct);
        var atividadeIds = atividades.Select(a => a.Id).ToList();
        var gheFuncoes = await _db.GheFuncoes.Where(gf => gf.GheId == ghe.Id)
            .Select(gf => new { gf.FuncaoId, gf.DescricaoAtividades }).ToListAsync(ct);
        var nomes = await NomesFuncoes.CarregarAsync(_db, gheFuncoes.Select(f => f.FuncaoId), ct);
        var registros = await _db.AtividadesFuncoes.Where(x => atividadeIds.Contains(x.AtividadeId)).ToListAsync(ct);
        var porPar = registros.ToDictionary(x => (x.AtividadeId, x.FuncaoId));

        var vinculos = await _db.AtividadeDocumentos.Where(d => atividadeIds.Contains(d.AtividadeId))
            .Select(d => new { d.AtividadeId, d.MaterialApoioId }).ToListAsync(ct);
        var docIds = vinculos.Select(v => v.MaterialApoioId).Distinct().ToList();
        var vigentes = (await _db.MateriaisApoio.Where(m => docIds.Contains(m.Id) && m.Situacao == SituacaoDocumentoSst.Vigente)
            .Select(m => m.Id).ToListAsync(ct)).ToHashSet();

        var dto = new MatrizAtividadesFuncaoDto { ObraId = request.ObraId, GheId = ghe.Id, GheNumero = ghe.Numero };
        foreach (var a in atividades)
            dto.Atividades.Add(new AtividadeMatrizDto
            {
                AtividadeId = a.Id,
                Nome = a.Nome,
                Procedimentos = vinculos.Count(v => v.AtividadeId == a.Id && vigentes.Contains(v.MaterialApoioId)),
            });

        foreach (var f in gheFuncoes.OrderBy(f => nomes.GetValueOrDefault(f.FuncaoId) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var funcao = new FuncaoMatrizDto
            {
                FuncaoId = f.FuncaoId,
                Nome = nomes.GetValueOrDefault(f.FuncaoId) ?? "(função removida)",
                DescricaoNoPgr = f.DescricaoAtividades,
            };
            foreach (var a in atividades)
            {
                var celula = new CelulaMatrizDto { AtividadeId = a.Id, FuncaoId = f.FuncaoId };
                if (porPar.TryGetValue((a.Id, f.FuncaoId), out var r))
                {
                    celula.Estado = EstadoAtividadeFuncao.De(r.Status);
                    celula.Origem = r.OrigemVinculo.ToString();
                    celula.JustificativaIa = r.JustificativaIa;
                    celula.ValidadoPorNome = r.ValidadoPorNome;
                    celula.ValidadoEmUtc = r.ValidadoEmUtc;
                }
                switch (celula.Estado)
                {
                    case EstadoAtividadeFuncao.Validada: funcao.Validadas++; break;
                    case EstadoAtividadeFuncao.Pendente: funcao.Pendentes++; break;
                    case EstadoAtividadeFuncao.Descartada: funcao.Descartadas++; break;
                    default: funcao.SemDecisao++; break;
                }
                dto.Celulas.Add(celula);
            }
            dto.Funcoes.Add(funcao);
        }
        return dto;
    }
}
