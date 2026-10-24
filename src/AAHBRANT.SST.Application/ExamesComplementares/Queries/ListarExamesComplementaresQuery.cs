using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.ExamesComplementares.Queries;

public record ListarExamesComplementaresQuery(Guid? TrabalhadorId = null) : IRequest<List<ExameComplementarDto>>;

public class ListarExamesComplementaresQueryHandler : IRequestHandler<ListarExamesComplementaresQuery, List<ExameComplementarDto>>
{
    private readonly IAppDbContext _db;

    public ListarExamesComplementaresQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ExameComplementarDto>> Handle(ListarExamesComplementaresQuery request, CancellationToken ct)
    {
        var query = _db.ExamesComplementares.NoEscopoDoTrabalhador(_db, e => e.TrabalhadorId);

        if (request.TrabalhadorId.HasValue)
            query = query.Where(e => e.TrabalhadorId == request.TrabalhadorId.Value);

        return await query
            .OrderByDescending(e => e.DataValidade)
            .Select(e => new ExameComplementarDto
            {
                Id = e.Id,
                TrabalhadorId = e.TrabalhadorId,
                AsoId = e.AsoId,
                Tipo = e.Tipo,
                CodigoExame = e.CodigoExame,
                NomeExame = e.NomeExame,
                DataRealizacao = e.DataRealizacao,
                DataValidade = e.DataValidade,
                Resultado = e.Resultado,
                Observacoes = e.Observacoes,
                ResponsavelTecnico = e.ResponsavelTecnico
            })
            .ToListAsync(ct);
    }
}
