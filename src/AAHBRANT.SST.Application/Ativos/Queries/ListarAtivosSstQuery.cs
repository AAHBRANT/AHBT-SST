using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ativos.Queries;

public record ListarAtivosSstQuery(Guid? ObraId = null, TipoAtivo? TipoAtivo = null) : IRequest<List<AtivoSstDto>>;

public class ListarAtivosSstQueryHandler : IRequestHandler<ListarAtivosSstQuery, List<AtivoSstDto>>
{
    private readonly IAppDbContext _db;

    public ListarAtivosSstQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AtivoSstDto>> Handle(ListarAtivosSstQuery request, CancellationToken ct)
    {
        var query = _db.AtivosSst.Include(a => a.Obra).AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(a => a.ObraId == request.ObraId.Value);

        if (request.TipoAtivo.HasValue)
            query = query.Where(a => a.TipoAtivo == request.TipoAtivo.Value);

        return await query
            .OrderBy(a => a.Descricao)
            .Select(a => new AtivoSstDto
            {
                Id = a.Id,
                ObraId = a.ObraId,
                ObraNome = a.Obra != null ? a.Obra.Nome : string.Empty,
                TipoAtivo = a.TipoAtivo,
                Identificacao = a.Identificacao,
                Descricao = a.Descricao,
                Localizacao = a.Localizacao,
                DataValidade = a.DataValidade,
                Observacoes = a.Observacoes
            })
            .ToListAsync(ct);
    }
}
