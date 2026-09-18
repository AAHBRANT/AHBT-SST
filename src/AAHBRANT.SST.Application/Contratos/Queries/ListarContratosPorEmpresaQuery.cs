using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Contratos.Queries;

public record ListarContratosPorEmpresaQuery(Guid EmpresaId) : IRequest<List<ContratoDto>>;

public class ListarContratosPorEmpresaQueryHandler : IRequestHandler<ListarContratosPorEmpresaQuery, List<ContratoDto>>
{
    private readonly IAppDbContext _db;
    public ListarContratosPorEmpresaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ContratoDto>> Handle(ListarContratosPorEmpresaQuery request, CancellationToken ct)
        => await _db.Contratos
            .Where(c => c.EmpresaId == request.EmpresaId)
            .OrderByDescending(c => c.DataInicioVigencia)
            .Select(c => new ContratoDto(
                c.Id, c.EmpresaId, c.ObraId, c.Obra!.Nome, c.NumeroContrato,
                c.DataInicioVigencia, c.DataFimVigencia, c.Status.ToString()))
            .ToListAsync(ct);
}
