using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Higienizacao.Queries;

public record ListarItensHigienizacaoQuery(Guid? ObraId = null) : IRequest<List<ItemHigienizacaoDto>>;

public class ListarItensHigienizacaoQueryHandler : IRequestHandler<ListarItensHigienizacaoQuery, List<ItemHigienizacaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarItensHigienizacaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ItemHigienizacaoDto>> Handle(ListarItensHigienizacaoQuery request, CancellationToken ct)
    {
        var query = _db.ItensHigienizacao
            .Include(i => i.Obra)
            .Include(i => i.Registros)
            .AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(i => i.ObraId == request.ObraId.Value);

        var lista = await query.OrderBy(i => i.Nome).ToListAsync(ct);

        return lista.Select(MapearParaDto).ToList();
    }

    internal static ItemHigienizacaoDto MapearParaDto(Domain.Entidades.ItemHigienizacao item)
    {
        var registrosAtivos = item.Registros.Where(r => r.Ativo).ToList();
        var ultimaHigienizacao = registrosAtivos.Count > 0 ? registrosAtivos.Max(r => r.DataHora) : (DateTime?)null;
        var baseCalculo = ultimaHigienizacao ?? item.CreatedAtUtc;

        return new ItemHigienizacaoDto
        {
            Id = item.Id,
            ObraId = item.ObraId,
            ObraNome = item.Obra?.Nome ?? string.Empty,
            Nome = item.Nome,
            Local = item.Local,
            PeriodicidadeDias = item.PeriodicidadeDias,
            UltimaHigienizacaoEm = ultimaHigienizacao,
            ProximoVencimentoEm = baseCalculo.AddDays(item.PeriodicidadeDias),
            TotalRegistros = registrosAtivos.Count,
        };
    }
}
