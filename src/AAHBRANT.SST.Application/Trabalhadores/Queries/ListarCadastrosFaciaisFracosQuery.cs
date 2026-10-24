using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

public record CadastroFacialFracoDto(
    Guid TrabalhadorId,
    string Nome,
    string? Matricula,
    Guid ObraId,
    string ObraNome,
    int Falhas,
    string UltimoMotivo,
    DateTime UltimaFalhaEm,
    DateTime CadastroEm);

// Funcionários cujo reconhecimento facial falhou várias vezes (por padrão 3 ou mais em 30 dias) contra o
// cadastro atual: candidatos a refazer a foto. Só contam as falhas POSTERIORES ao cadastro vigente,
// então quem já refez sai da lista sozinho. O escopo por obra vem dos filtros globais do DbContext.
public record ListarCadastrosFaciaisFracosQuery(Guid? ObraId = null, int Dias = 30, int MinimoFalhas = 3)
    : IRequest<List<CadastroFacialFracoDto>>;

public class ListarCadastrosFaciaisFracosQueryHandler : IRequestHandler<ListarCadastrosFaciaisFracosQuery, List<CadastroFacialFracoDto>>
{
    private readonly IAppDbContext _db;

    public ListarCadastrosFaciaisFracosQueryHandler(IAppDbContext db) => _db = db;

    public static string DescreverMotivo(MotivoFalhaFacial motivo) => motivo switch
    {
        MotivoFalhaFacial.ConfiancaBaixa => "Baixa confiança",
        MotivoFalhaFacial.RostoAmbiguo => "Rosto parecido com outra pessoa",
        _ => "Rosto não reconhecido",
    };

    public async Task<List<CadastroFacialFracoDto>> Handle(ListarCadastrosFaciaisFracosQuery request, CancellationToken ct)
    {
        var desde = DateTime.UtcNow.AddDays(-request.Dias);

        // Só colunas escalares: sem navegação (o filtro global de Trabalhador não derruba a linha) e
        // sem os bytes da foto de cadastro.
        var falhas = await _db.FalhasReconhecimentoFacial
            .Where(f => f.TrabalhadorId != null && f.OcorridaEm >= desde && (request.ObraId == null || f.ObraId == request.ObraId))
            .Select(f => new { TrabalhadorId = f.TrabalhadorId!.Value, f.ObraId, f.Motivo, f.OcorridaEm })
            .ToListAsync(ct);
        if (falhas.Count == 0) return new List<CadastroFacialFracoDto>();

        var ids = falhas.Select(f => f.TrabalhadorId).Distinct().ToList();

        var cadastros = await _db.FotosCadastroFacial
            .Where(f => ids.Contains(f.TrabalhadorId))
            .Select(f => new { f.TrabalhadorId, f.CapturadaEm })
            .ToListAsync(ct);
        var cadastroAtual = cadastros
            .GroupBy(c => c.TrabalhadorId)
            .ToDictionary(g => g.Key, g => g.Max(c => c.CapturadaEm));

        var candidatos = falhas
            .Where(f => cadastroAtual.TryGetValue(f.TrabalhadorId, out var cadastroEm) && f.OcorridaEm > cadastroEm)
            .GroupBy(f => f.TrabalhadorId)
            .Where(g => g.Count() >= request.MinimoFalhas)
            .ToList();
        if (candidatos.Count == 0) return new List<CadastroFacialFracoDto>();

        var idsFinais = candidatos.Select(g => g.Key).ToList();
        var trabalhadores = await _db.Trabalhadores
            .Where(t => idsFinais.Contains(t.Id))
            .Select(t => new { t.Id, t.Nome, t.Matricula, t.ObraId })
            .ToListAsync(ct);
        var obraIds = trabalhadores.Select(t => t.ObraId).Distinct().ToList();
        var obras = await _db.Obras.Where(o => obraIds.Contains(o.Id)).Select(o => new { o.Id, o.Nome }).ToListAsync(ct);

        return candidatos
            .Select(g =>
            {
                var t = trabalhadores.FirstOrDefault(x => x.Id == g.Key);
                if (t is null) return null;
                var ultima = g.OrderByDescending(f => f.OcorridaEm).First();
                return new CadastroFacialFracoDto(
                    t.Id, t.Nome, t.Matricula, t.ObraId,
                    obras.FirstOrDefault(o => o.Id == t.ObraId)?.Nome ?? string.Empty,
                    g.Count(), DescreverMotivo(ultima.Motivo), ultima.OcorridaEm, cadastroAtual[g.Key]);
            })
            .Where(d => d is not null)
            .Select(d => d!)
            .OrderByDescending(d => d.Falhas)
            .ThenByDescending(d => d.UltimaFalhaEm)
            .ToList();
    }
}
