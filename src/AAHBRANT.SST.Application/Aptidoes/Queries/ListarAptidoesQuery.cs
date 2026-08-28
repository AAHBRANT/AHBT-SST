using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aptidoes.Queries;

public record ListarAptidoesQuery(Guid? TrabalhadorId = null) : IRequest<List<AptidaoDto>>;

public class ListarAptidoesQueryHandler : IRequestHandler<ListarAptidoesQuery, List<AptidaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarAptidoesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AptidaoDto>> Handle(ListarAptidoesQuery request, CancellationToken ct)
    {
        var query = _db.AptidoesAtividadeEspecifica.AsQueryable();

        if (request.TrabalhadorId.HasValue)
            query = query.Where(a => a.TrabalhadorId == request.TrabalhadorId.Value);

        return await query
            .OrderByDescending(a => a.DataAvaliacao)
            .Select(a => new AptidaoDto
            {
                Id = a.Id,
                TrabalhadorId = a.TrabalhadorId,
                AtividadeCritica = a.AtividadeCritica,
                Aptidao = a.Aptidao,
                DataAvaliacao = a.DataAvaliacao,
                DataValidade = a.DataValidade,
                MedicoResponsavel = a.MedicoResponsavel,
                Observacoes = a.Observacoes
            })
            .ToListAsync(ct);
    }
}
