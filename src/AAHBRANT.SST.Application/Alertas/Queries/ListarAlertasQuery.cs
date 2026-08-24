using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Queries;

public record ListarAlertasQuery(
    StatusAlerta? Status,
    SeveridadeAlerta? Severidade,
    Guid? ObraId,
    Guid? TrabalhadorId) : IRequest<List<AlertaDto>>;

public class ListarAlertasQueryHandler : IRequestHandler<ListarAlertasQuery, List<AlertaDto>>
{
    private readonly IAppDbContext _db;

    public ListarAlertasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AlertaDto>> Handle(ListarAlertasQuery request, CancellationToken ct)
    {
        var query = _db.Alertas.AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(a => a.Status == request.Status.Value);
        if (request.Severidade.HasValue)
            query = query.Where(a => a.Severidade == request.Severidade.Value);
        if (request.ObraId.HasValue)
            query = query.Where(a => a.ObraId == request.ObraId.Value);
        if (request.TrabalhadorId.HasValue)
            query = query.Where(a => a.TrabalhadorId == request.TrabalhadorId.Value);

        return await query
            .Include(a => a.Trabalhador)
            .Include(a => a.Obra)
            .Include(a => a.DestinatarioUsuario)
            .Include(a => a.EscalonadoParaUsuario)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new AlertaDto
            {
                Id = a.Id,
                Tipo = a.Tipo,
                Severidade = a.Severidade,
                Status = a.Status,
                Titulo = a.Titulo,
                Descricao = a.Descricao,
                EntidadeOrigemTipo = a.EntidadeOrigemTipo,
                EntidadeOrigemId = a.EntidadeOrigemId,
                TrabalhadorId = a.TrabalhadorId,
                TrabalhadorNome = a.Trabalhador != null ? a.Trabalhador.Nome : null,
                ObraId = a.ObraId,
                ObraNome = a.Obra != null ? a.Obra.Nome : null,
                DestinatarioUsuarioId = a.DestinatarioUsuarioId,
                DestinatarioUsuarioNome = a.DestinatarioUsuario != null ? a.DestinatarioUsuario.Nome : null,
                DataLimiteTratamento = a.DataLimiteTratamento,
                EscalonadoParaUsuarioId = a.EscalonadoParaUsuarioId,
                EscalonadoParaUsuarioNome = a.EscalonadoParaUsuario != null ? a.EscalonadoParaUsuario.Nome : null,
                DataEscalonamento = a.DataEscalonamento,
                CreatedAtUtc = a.CreatedAtUtc,
            })
            .ToListAsync(ct);
    }
}
