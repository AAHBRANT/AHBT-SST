using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.ExamesComplementares.Queries;

public record ObterExameComplementarPorIdQuery(Guid Id) : IRequest<ExameComplementarDto?>;

public class ObterExameComplementarPorIdQueryHandler : IRequestHandler<ObterExameComplementarPorIdQuery, ExameComplementarDto?>
{
    private readonly IAppDbContext _db;

    public ObterExameComplementarPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ExameComplementarDto?> Handle(ObterExameComplementarPorIdQuery request, CancellationToken ct)
    {
        return await _db.ExamesComplementares
            .Where(e => e.Id == request.Id)
            .Select(e => new ExameComplementarDto
            {
                Id = e.Id,
                TrabalhadorId = e.TrabalhadorId,
                AsoId = e.AsoId,
                Tipo = e.Tipo,
                DataRealizacao = e.DataRealizacao,
                DataValidade = e.DataValidade,
                Resultado = e.Resultado,
                Observacoes = e.Observacoes,
                ResponsavelTecnico = e.ResponsavelTecnico
            })
            .FirstOrDefaultAsync(ct);
    }
}
