using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos.Queries;

public record ObterTreinamentoPorIdQuery(Guid Id) : IRequest<TreinamentoDto?>;

public class ObterTreinamentoPorIdQueryHandler : IRequestHandler<ObterTreinamentoPorIdQuery, TreinamentoDto?>
{
    private readonly IAppDbContext _db;
    public ObterTreinamentoPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<TreinamentoDto?> Handle(ObterTreinamentoPorIdQuery request, CancellationToken ct)
        => await _db.Treinamentos
            .Where(x => x.Id == request.Id)
            .Select(x => new TreinamentoDto(
                x.Id,
                x.TrabalhadorId,
                x.CursoTreinamentoId,
                x.DataRealizacao,
                x.DataValidade,
                x.CargaHorariaRealizada,
                x.InstituicaoInstrutor,
                x.NumeroCertificado))
            .FirstOrDefaultAsync(ct);
}
