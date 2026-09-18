using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
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

        // Pendências de Terceirizado só fazem sentido para quem tem esse vínculo — sem esta trava, um
        // CLT/Autônomo/Estagiário chamando esse endpoint (agora exposto de verdade pela aba Terceirizado
        // na ficha do trabalhador) receberia pendências calculadas para um vínculo que não é o dele.
        if (trabalhador.Vinculo != TipoVinculo.Terceirizado)
            return new StatusLiberacaoTerceirizadoDto(trabalhador.Id, StatusLiberacaoTerceirizado.Liberada, new List<string>());

        var pendencias = await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(_db, trabalhador.Id, trabalhador.FuncaoId, ct);
        var status = pendencias.Count == 0 ? StatusLiberacaoTerceirizado.Liberada : StatusLiberacaoTerceirizado.Pendente;

        return new StatusLiberacaoTerceirizadoDto(trabalhador.Id, status, pendencias);
    }
}
