using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Queries;

public record ObterAlertaPorIdQuery(Guid Id) : IRequest<AlertaDto?>;

public class ObterAlertaPorIdQueryHandler : IRequestHandler<ObterAlertaPorIdQuery, AlertaDto?>
{
    private readonly IAppDbContext _db;

    public ObterAlertaPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AlertaDto?> Handle(ObterAlertaPorIdQuery request, CancellationToken ct)
    {
        return await _db.Alertas
            .Include(a => a.Trabalhador)
            .Include(a => a.Obra)
            .Include(a => a.DestinatarioUsuario)
            .Include(a => a.EscalonadoParaUsuario)
            .Where(a => a.Id == request.Id)
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
            .FirstOrDefaultAsync(ct);
    }
}
