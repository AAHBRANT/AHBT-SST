using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Queries;

public record ListarAprsQuery(Guid? AtividadeId = null) : IRequest<List<AprDto>>;

public class ListarAprsQueryHandler : IRequestHandler<ListarAprsQuery, List<AprDto>>
{
    private readonly IAppDbContext _db;

    public ListarAprsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AprDto>> Handle(ListarAprsQuery request, CancellationToken ct)
    {
        var query = _db.Aprs
            .Include(a => a.Atividade)
            .Include(a => a.Equipe)
            .Include(a => a.AprovadoPorUsuario)
            .AsQueryable();

        if (request.AtividadeId.HasValue)
            query = query.Where(a => a.AtividadeId == request.AtividadeId.Value);

        var aprs = await query.OrderByDescending(a => a.CreatedAtUtc).ToListAsync(ct);

        return aprs.Select(a => new AprDto
        {
            Id = a.Id,
            AtividadeId = a.AtividadeId,
            AtividadeNome = a.Atividade?.Nome ?? string.Empty,
            Local = a.Local,
            EquipeId = a.EquipeId,
            EquipeNome = a.Equipe?.Nome,
            Data = a.Data,
            Validade = a.Validade,
            Status = a.Status,
            AprovadoPorUsuarioId = a.AprovadoPorUsuarioId,
            AprovadoPorUsuarioNome = a.AprovadoPorUsuario?.Nome,
            DataAprovacao = a.DataAprovacao,
            MotivoReprovacao = a.MotivoReprovacao
        }).ToList();
    }
}
