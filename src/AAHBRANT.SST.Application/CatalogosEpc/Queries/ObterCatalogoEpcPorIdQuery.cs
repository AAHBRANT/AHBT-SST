using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpc.Queries;

public record ObterCatalogoEpcPorIdQuery(Guid Id) : IRequest<CatalogoEpcDto?>;

public class ObterCatalogoEpcPorIdQueryHandler : IRequestHandler<ObterCatalogoEpcPorIdQuery, CatalogoEpcDto?>
{
    private readonly IAppDbContext _db;
    public ObterCatalogoEpcPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<CatalogoEpcDto?> Handle(ObterCatalogoEpcPorIdQuery request, CancellationToken ct)
        => await _db.CatalogoEpcs
            .Where(x => x.Id == request.Id && x.Ativo)
            .Select(x => new CatalogoEpcDto(x.Id, x.Nome, x.Fabricante, x.CertificadoAprovacaoNumero, x.CertificadoAprovacaoValidade, x.VidaUtilEmMeses, x.Estoques.Sum(e => (int?)e.Saldo) ?? 0, x.FotoConteudo != null))
            .FirstOrDefaultAsync(ct);
}
