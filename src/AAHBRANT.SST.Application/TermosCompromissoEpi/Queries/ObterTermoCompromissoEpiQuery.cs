using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using MediatR;

namespace AAHBRANT.SST.Application.TermosCompromissoEpi.Queries;

public record ObterTermoCompromissoEpiQuery(Guid TrabalhadorId) : IRequest<TermoCompromissoEpiDto>;

public class ObterTermoCompromissoEpiQueryHandler : IRequestHandler<ObterTermoCompromissoEpiQuery, TermoCompromissoEpiDto>
{
    private readonly IAppDbContext _db;
    public ObterTermoCompromissoEpiQueryHandler(IAppDbContext db) => _db = db;

    public async Task<TermoCompromissoEpiDto> Handle(ObterTermoCompromissoEpiQuery request, CancellationToken ct)
    {
        await _db.GarantirTrabalhadorNoEscopoAsync(request.TrabalhadorId, ct);
        return await TermoCompromissoEpiConsulta.ObterAsync(_db, request.TrabalhadorId, ct);
    }
}
