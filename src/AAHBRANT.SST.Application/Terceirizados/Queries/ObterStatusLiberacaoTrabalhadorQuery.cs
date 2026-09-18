using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Queries;

public record ObterStatusLiberacaoTrabalhadorQuery(Guid TrabalhadorId) : IRequest<StatusLiberacaoTerceirizadoDto>;

public class ObterStatusLiberacaoTrabalhadorQueryHandler
    : IRequestHandler<ObterStatusLiberacaoTrabalhadorQuery, StatusLiberacaoTerceirizadoDto>
{
    private readonly IAppDbContext _db;
    public ObterStatusLiberacaoTrabalhadorQueryHandler(IAppDbContext db) => _db = db;

    public async Task<StatusLiberacaoTerceirizadoDto> Handle(ObterStatusLiberacaoTrabalhadorQuery request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

        var pendencias = await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(_db, trabalhador.Id, trabalhador.FuncaoId, ct);
        var status = pendencias.Count == 0 ? StatusLiberacaoTerceirizado.Liberada : StatusLiberacaoTerceirizado.Pendente;

        return new StatusLiberacaoTerceirizadoDto(trabalhador.Id, status, pendencias);
    }
}
