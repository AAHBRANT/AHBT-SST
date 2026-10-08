using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;

namespace AAHBRANT.SST.Application.TermosCompromissoEpi.Queries;

public record ObterTermoCompromissoEpiQuery(Guid TrabalhadorId) : IRequest<TermoCompromissoEpiDto>;

public class ObterTermoCompromissoEpiQueryHandler : IRequestHandler<ObterTermoCompromissoEpiQuery, TermoCompromissoEpiDto>
{
    private readonly IAppDbContext _db;
    public ObterTermoCompromissoEpiQueryHandler(IAppDbContext db) => _db = db;

    public Task<TermoCompromissoEpiDto> Handle(ObterTermoCompromissoEpiQuery request, CancellationToken ct)
        => TermoCompromissoEpiConsulta.ObterAsync(_db, request.TrabalhadorId, ct);
}
