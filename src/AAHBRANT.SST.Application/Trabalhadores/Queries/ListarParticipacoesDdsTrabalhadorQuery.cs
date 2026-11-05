using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

// Histórico de DDS do trabalhador no perfil (aba Treinamentos): todos os DDS da obra dele desde a
// admissão — o mesmo universo do card "Assiduidade em DDS" — com presença ou falta em cada um.
public record ListarParticipacoesDdsTrabalhadorQuery(Guid TrabalhadorId) : IRequest<List<ParticipacaoDdsDto>?>;

public record ParticipacaoDdsDto(
    Guid DdsId,
    DateTime Data,
    string? NumeroDocumento,
    string Tema,
    string? ResponsavelNome,
    StatusDds Status,
    bool SemExpediente,
    string? MotivoSemExpediente,
    int QuantidadeAtividades,
    int QuantidadeParticipantes,
    bool Presente,
    TipoFotoParticipante? MetodoPresenca,
    double? ScoreConfianca,
    DateTime? PresencaRegistradaEm);

public class ListarParticipacoesDdsTrabalhadorQueryHandler
    : IRequestHandler<ListarParticipacoesDdsTrabalhadorQuery, List<ParticipacaoDdsDto>?>
{
    private readonly IAppDbContext _db;
    public ListarParticipacoesDdsTrabalhadorQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ParticipacaoDdsDto>?> Handle(ListarParticipacoesDdsTrabalhadorQuery request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.AsNoTracking()
            .NoEscopoDoTrabalhador(_db, t => t.Id)
            .Where(t => t.Id == request.TrabalhadorId)
            .Select(t => new { t.ObraId, t.DataAdmissao })
            .FirstOrDefaultAsync(ct);
        if (trabalhador is null) return null;

        // Sem projetar navegações (Responsável/Tema): filtros globais nelas transformariam o join em
        // INNER e esconderiam o DDS inteiro. Os nomes são resolvidos à parte, ignorando filtros.
        var dds = await _db.Dds.AsNoTracking()
            .Where(d => d.ObraId == trabalhador.ObraId && d.Data >= trabalhador.DataAdmissao)
            .OrderByDescending(d => d.Data)
            .Select(d => new
            {
                d.Id,
                d.Data,
                d.NumeroDocumento,
                d.CatalogoTemaDdsId,
                d.TemaLivreNome,
                d.ResponsavelUsuarioId,
                d.Status,
                d.SemExpediente,
                d.MotivoSemExpediente,
                QuantidadeAtividades = d.Atividades.Count,
                QuantidadeParticipantes = d.Participantes.Count,
                AtividadesNomes = d.Atividades.OrderBy(a => a.Ordem).Select(a => a.AtividadeNome).ToList(),
                Presenca = d.Participantes
                    .Where(p => p.TrabalhadorId == request.TrabalhadorId)
                    .Select(p => new { p.FotoTipo, p.ScoreConfianca, p.CreatedAtUtc })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var temaIds = dds.Where(d => d.CatalogoTemaDdsId is not null).Select(d => d.CatalogoTemaDdsId!.Value).Distinct().ToList();
        var temas = await _db.CatalogosTemaDds.IgnoreQueryFilters().AsNoTracking()
            .Where(t => temaIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Nome, ct);
        var usuarioIds = dds.Select(d => d.ResponsavelUsuarioId).Distinct().ToList();
        var usuarios = await _db.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .Where(u => usuarioIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nome, ct);

        return dds.Select(d =>
        {
            var tema = d.CatalogoTemaDdsId is Guid tid && temas.TryGetValue(tid, out var nome) ? nome
                : !string.IsNullOrWhiteSpace(d.TemaLivreNome) ? d.TemaLivreNome!
                : d.AtividadesNomes.Count > 0 ? string.Join(", ", d.AtividadesNomes.Where(n => !string.IsNullOrWhiteSpace(n)))
                : d.SemExpediente ? "Sem expediente" : "DDS";
            return new ParticipacaoDdsDto(
                d.Id,
                d.Data,
                d.NumeroDocumento,
                tema,
                usuarios.TryGetValue(d.ResponsavelUsuarioId, out var resp) ? resp : null,
                d.Status,
                d.SemExpediente,
                d.MotivoSemExpediente,
                d.QuantidadeAtividades,
                d.QuantidadeParticipantes,
                d.Presenca is not null,
                d.Presenca?.FotoTipo,
                d.Presenca?.ScoreConfianca,
                d.Presenca?.CreatedAtUtc);
        }).ToList();
    }
}
