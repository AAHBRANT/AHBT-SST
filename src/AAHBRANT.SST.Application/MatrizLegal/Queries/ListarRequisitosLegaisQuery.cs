using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MatrizLegal.Queries;

public record ListarRequisitosLegaisQuery(
    string? Norma,
    string? Tema,
    bool? Aplicabilidade,
    StatusRequisitoLegal? Status,
    Guid? ObraId) : IRequest<List<RequisitoLegalDto>>;

public class ListarRequisitosLegaisQueryHandler
    : IRequestHandler<ListarRequisitosLegaisQuery, List<RequisitoLegalDto>>
{
    private readonly IAppDbContext _db;

    public ListarRequisitosLegaisQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<RequisitoLegalDto>> Handle(ListarRequisitosLegaisQuery request, CancellationToken ct)
    {
        var query = _db.RequisitosLegais.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Norma))
            query = query.Where(r => r.Norma.Contains(request.Norma));

        if (!string.IsNullOrWhiteSpace(request.Tema))
            query = query.Where(r => r.Tema.Contains(request.Tema));

        if (request.Aplicabilidade.HasValue)
            query = query.Where(r => r.Aplicabilidade == request.Aplicabilidade.Value);

        if (request.Status.HasValue)
            query = query.Where(r => r.Status == request.Status.Value);

        if (request.ObraId.HasValue)
            query = query.Where(r => r.ObraId == request.ObraId.Value);

        return await query
            .Include(r => r.ResponsavelUsuario)
            .Include(r => r.Obra)
            .OrderBy(r => r.Norma).ThenBy(r => r.Codigo)
            .Select(r => new RequisitoLegalDto
            {
                Id = r.Id,
                Codigo = r.Codigo,
                Norma = r.Norma,
                Item = r.Item,
                Tema = r.Tema,
                Requisito = r.Requisito,
                Aplicabilidade = r.Aplicabilidade,
                Justificativa = r.Justificativa,
                Evidencia = r.Evidencia,
                ResponsavelUsuarioId = r.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = r.ResponsavelUsuario != null ? r.ResponsavelUsuario.Nome : null,
                Periodicidade = r.Periodicidade,
                Prazo = r.Prazo,
                Status = r.Status,
                UltimaRevisao = r.UltimaRevisao,
                ProximaRevisao = r.ProximaRevisao,
                ObraId = r.ObraId,
                ObraNome = r.Obra != null ? r.Obra.Nome : null,
            })
            .ToListAsync(ct);
    }
}
