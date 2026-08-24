using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpi.Queries;

public record ListarCatalogosEpiQuery : IRequest<List<CatalogoEpiDto>>;

public class ListarCatalogosEpiQueryHandler : IRequestHandler<ListarCatalogosEpiQuery, List<CatalogoEpiDto>>
{
    private readonly IAppDbContext _db;
    public ListarCatalogosEpiQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CatalogoEpiDto>> Handle(ListarCatalogosEpiQuery request, CancellationToken ct)
        => await _db.CatalogoEpis
            .OrderBy(x => x.Nome)
            .Select(x => new CatalogoEpiDto(x.Id, x.Nome, x.CertificadoAprovacaoNumero, x.CertificadoAprovacaoValidade, x.VidaUtilEmMeses))
            .ToListAsync(ct);
}
