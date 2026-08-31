using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Queries;

public record ListarDimensionamentosCipaQuery(Guid? ObraId = null) : IRequest<List<DimensionamentoCipaDto>>;

public class ListarDimensionamentosCipaQueryHandler : IRequestHandler<ListarDimensionamentosCipaQuery, List<DimensionamentoCipaDto>>
{
    private readonly IAppDbContext _db;
    public ListarDimensionamentosCipaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<DimensionamentoCipaDto>> Handle(ListarDimensionamentosCipaQuery request, CancellationToken ct)
    {
        var query = _db.DimensionamentosCipa.Include(d => d.Obra).AsQueryable();
        if (request.ObraId.HasValue) query = query.Where(d => d.ObraId == request.ObraId.Value);

        return await query
            .OrderByDescending(d => d.DataCalculo)
            .Select(d => new DimensionamentoCipaDto
            {
                Id = d.Id,
                ObraId = d.ObraId,
                ObraNome = d.Obra!.Nome,
                Cnae = d.Cnae,
                GrauRisco = d.GrauRisco,
                NumeroFuncionarios = d.NumeroFuncionarios,
                NumeroTitulares = d.NumeroTitulares,
                NumeroSuplentes = d.NumeroSuplentes,
                DataCalculo = d.DataCalculo,
                Observacoes = d.Observacoes,
            })
            .ToListAsync(ct);
    }
}
