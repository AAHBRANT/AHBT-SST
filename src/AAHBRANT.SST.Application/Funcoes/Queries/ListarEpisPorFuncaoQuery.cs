using AAHBRANT.SST.Application.CatalogosEpi;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

// ObraId: a matriz que vale para a obra (a dela, ou a antiga enquanto ela não tiver a própria).
public record ListarEpisPorFuncaoQuery(Guid FuncaoId, Guid? ObraId = null) : IRequest<List<CatalogoEpiDto>>;

public class ListarEpisPorFuncaoQueryHandler : IRequestHandler<ListarEpisPorFuncaoQuery, List<CatalogoEpiDto>>
{
    private readonly IAppDbContext _db;
    public ListarEpisPorFuncaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CatalogoEpiDto>> Handle(ListarEpisPorFuncaoQuery request, CancellationToken ct)
        => await (request.ObraId is { } obraId ? _db.MatrizEpiFuncoes.ParaObra(obraId) : _db.MatrizEpiFuncoes.Where(m => m.ObraId == null))
            .Where(m => m.FuncaoId == request.FuncaoId)
            .OrderBy(m => m.CatalogoEpi!.Nome)
            .Select(m => new CatalogoEpiDto(
                m.CatalogoEpi!.Id,
                m.CatalogoEpi!.Nome,
                m.CatalogoEpi!.Fabricante,
                m.CatalogoEpi!.CertificadoAprovacaoNumero,
                m.CatalogoEpi!.CertificadoAprovacaoValidade,
                m.CatalogoEpi!.VidaUtilEmMeses,
                m.CatalogoEpi!.Estoques.Sum(e => (int?)e.Saldo) ?? 0,
                m.CatalogoEpi!.FotoConteudo != null,
                m.CatalogoEpi!.ObraId))
            .ToListAsync(ct);
}
