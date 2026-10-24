using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

// Confiança em % (0 a 100). TrabalhadorNome nulo = rosto não atribuído a ninguém (abaixo da confiança mínima).
public record FalhaReconhecimentoFacialDto(
    Guid Id, DateTime OcorridaEm, Guid ObraId, string ObraNome, Guid? TrabalhadorId, string? TrabalhadorNome, string Motivo, double? ConfiancaPercentual);

// Lista de cada falha de reconhecimento facial, para conferir quem está falhando e com que confiança sem
// precisar de acesso ao banco (12/10). O escopo por obra vem do filtro global das falhas. As falhas ficam
// guardadas por 90 dias (AzureFaceAutenticacaoStrategy).
public record ListarFalhasReconhecimentoFacialQuery(Guid? ObraId, int Dias = 30) : IRequest<List<FalhaReconhecimentoFacialDto>>;

public class ListarFalhasReconhecimentoFacialQueryHandler : IRequestHandler<ListarFalhasReconhecimentoFacialQuery, List<FalhaReconhecimentoFacialDto>>
{
    private const int Limite = 2000;
    private readonly IAppDbContext _db;

    public ListarFalhasReconhecimentoFacialQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<FalhaReconhecimentoFacialDto>> Handle(ListarFalhasReconhecimentoFacialQuery request, CancellationToken ct)
    {
        var desde = DateTime.UtcNow.AddDays(-Math.Clamp(request.Dias, 1, 90));
        var consulta = _db.FalhasReconhecimentoFacial.Where(f => f.OcorridaEm >= desde);
        if (request.ObraId is { } obraId)
            consulta = consulta.Where(f => f.ObraId == obraId);

        var falhas = await consulta
            .OrderByDescending(f => f.OcorridaEm)
            .Take(Limite)
            .Select(f => new { f.Id, f.OcorridaEm, f.ObraId, f.TrabalhadorId, f.Motivo, f.Confianca })
            .ToListAsync(ct);

        var obraIds = falhas.Select(f => f.ObraId).Distinct().ToList();
        var trabalhadorIds = falhas.Where(f => f.TrabalhadorId != null).Select(f => f.TrabalhadorId!.Value).Distinct().ToList();
        // IgnoreQueryFilters: a falha de quem já foi desligado continua mostrando o nome.
        var obras = await _db.Obras.IgnoreQueryFilters().Where(o => obraIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Nome, ct);
        var nomes = await _db.Trabalhadores.IgnoreQueryFilters().Where(t => trabalhadorIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nome, ct);

        return falhas.Select(f => new FalhaReconhecimentoFacialDto(
                f.Id, f.OcorridaEm, f.ObraId, obras.GetValueOrDefault(f.ObraId, ""),
                f.TrabalhadorId, f.TrabalhadorId is { } t ? nomes.GetValueOrDefault(t) : null,
                ListarCadastrosFaciaisFracosQueryHandler.DescreverMotivo(f.Motivo),
                f.Confianca is { } c ? Math.Round(c * 100, 1) : null))
            .ToList();
    }
}
