using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades.Queries;

public record ListarNaoConformidadesQuery(StatusNaoConformidade? Status) : IRequest<List<NaoConformidadeDto>>;

public class ListarNaoConformidadesQueryHandler
    : IRequestHandler<ListarNaoConformidadesQuery, List<NaoConformidadeDto>>
{
    private readonly IAppDbContext _db;

    public ListarNaoConformidadesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<NaoConformidadeDto>> Handle(ListarNaoConformidadesQuery request, CancellationToken ct)
    {
        var query = _db.NaoConformidades.AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(n => n.Status == request.Status.Value);

        return await query
            .Include(n => n.Atividade)
            .Include(n => n.ResponsavelUsuario)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new NaoConformidadeDto
            {
                Id = n.Id,
                OrigemDeteccao = n.OrigemDeteccao,
                RequisitoRelacionado = n.RequisitoRelacionado,
                Descricao = n.Descricao,
                Local = n.Local,
                AtividadeId = n.AtividadeId,
                AtividadeNome = n.Atividade != null ? n.Atividade.Nome : null,
                RiscoId = n.RiscoId,
                ResponsavelUsuarioId = n.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = n.ResponsavelUsuario != null ? n.ResponsavelUsuario.Nome : null,
                Prazo = n.Prazo,
                Status = n.Status,
                DataConclusao = n.DataConclusao,
                ObservacoesEncerramento = n.ObservacoesEncerramento,
            })
            .ToListAsync(ct);
    }
}
