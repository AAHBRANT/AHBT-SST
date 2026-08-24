using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AcoesPlano.Queries;

public record ListarAcoesPlanoQuery(string OrigemTipo, Guid OrigemId) : IRequest<List<AcaoPlanoDto>>;

public class ListarAcoesPlanoQueryHandler : IRequestHandler<ListarAcoesPlanoQuery, List<AcaoPlanoDto>>
{
    private readonly IAppDbContext _db;

    public ListarAcoesPlanoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AcaoPlanoDto>> Handle(ListarAcoesPlanoQuery request, CancellationToken ct)
    {
        return await _db.AcoesPlano
            .Where(a => a.OrigemTipo == request.OrigemTipo && a.OrigemId == request.OrigemId)
            .Include(a => a.ResponsavelUsuario)
            .Include(a => a.ValidadoPorUsuario)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new AcaoPlanoDto
            {
                Id = a.Id,
                OrigemTipo = a.OrigemTipo,
                OrigemId = a.OrigemId,
                Tipo = a.Tipo,
                Descricao = a.Descricao,
                ResponsavelUsuarioId = a.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = a.ResponsavelUsuario != null ? a.ResponsavelUsuario.Nome : null,
                Prioridade = a.Prioridade,
                Prazo = a.Prazo,
                Status = a.Status,
                DataConclusao = a.DataConclusao,
                DataValidacao = a.DataValidacao,
                ValidadoPorUsuarioId = a.ValidadoPorUsuarioId,
                ValidadoPorUsuarioNome = a.ValidadoPorUsuario != null ? a.ValidadoPorUsuario.Nome : null,
            })
            .ToListAsync(ct);
    }
}
