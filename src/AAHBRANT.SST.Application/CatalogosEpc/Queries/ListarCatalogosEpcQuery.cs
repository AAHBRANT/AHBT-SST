using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpc.Queries;

public record ListarCatalogosEpcQuery : IRequest<List<CatalogoEpcDto>>;

public class ListarCatalogosEpcQueryHandler : IRequestHandler<ListarCatalogosEpcQuery, List<CatalogoEpcDto>>
{
    private readonly IAppDbContext _db;
    public ListarCatalogosEpcQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CatalogoEpcDto>> Handle(ListarCatalogosEpcQuery request, CancellationToken ct)
        => await _db.CatalogoEpcs
            .Where(x => x.Ativo)
            .OrderBy(x => x.Nome)
            .Select(x => new CatalogoEpcDto(x.Id, x.Nome, x.Fabricante, x.CertificadoAprovacaoNumero, x.CertificadoAprovacaoValidade, x.VidaUtilEmMeses, x.Estoques.Sum(e => (int?)e.Saldo) ?? 0, x.FotoConteudo != null))
            .ToListAsync(ct);
}
