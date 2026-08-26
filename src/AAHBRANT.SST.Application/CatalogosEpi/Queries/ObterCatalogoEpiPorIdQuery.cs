using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpi.Queries;

public record ObterCatalogoEpiPorIdQuery(Guid Id) : IRequest<CatalogoEpiDto?>;

public class ObterCatalogoEpiPorIdQueryHandler : IRequestHandler<ObterCatalogoEpiPorIdQuery, CatalogoEpiDto?>
{
    private readonly IAppDbContext _db;
    public ObterCatalogoEpiPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<CatalogoEpiDto?> Handle(ObterCatalogoEpiPorIdQuery request, CancellationToken ct)
        => await _db.CatalogoEpis
            .Where(x => x.Id == request.Id)
            .Select(x => new CatalogoEpiDto(x.Id, x.Nome, x.Fabricante, x.CertificadoAprovacaoNumero, x.CertificadoAprovacaoValidade, x.VidaUtilEmMeses, x.SaldoEstoque))
            .FirstOrDefaultAsync(ct);
}
