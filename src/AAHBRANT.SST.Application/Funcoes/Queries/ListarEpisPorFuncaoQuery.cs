using AAHBRANT.SST.Application.CatalogosEpi;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

public record ListarEpisPorFuncaoQuery(Guid FuncaoId) : IRequest<List<CatalogoEpiDto>>;

public class ListarEpisPorFuncaoQueryHandler : IRequestHandler<ListarEpisPorFuncaoQuery, List<CatalogoEpiDto>>
{
    private readonly IAppDbContext _db;
    public ListarEpisPorFuncaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CatalogoEpiDto>> Handle(ListarEpisPorFuncaoQuery request, CancellationToken ct)
        => await _db.MatrizEpiFuncoes
            .Where(m => m.FuncaoId == request.FuncaoId)
            .OrderBy(m => m.CatalogoEpi!.Nome)
            .Select(m => new CatalogoEpiDto(
                m.CatalogoEpi!.Id,
                m.CatalogoEpi!.Nome,
                m.CatalogoEpi!.Fabricante,
                m.CatalogoEpi!.CertificadoAprovacaoNumero,
                m.CatalogoEpi!.CertificadoAprovacaoValidade,
                m.CatalogoEpi!.VidaUtilEmMeses,
                m.CatalogoEpi!.SaldoEstoque))
            .ToListAsync(ct);
}
