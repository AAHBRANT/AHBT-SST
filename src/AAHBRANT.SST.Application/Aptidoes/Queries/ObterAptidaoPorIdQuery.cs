using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aptidoes.Queries;

public record ObterAptidaoPorIdQuery(Guid Id) : IRequest<AptidaoDto?>;

public class ObterAptidaoPorIdQueryHandler : IRequestHandler<ObterAptidaoPorIdQuery, AptidaoDto?>
{
    private readonly IAppDbContext _db;

    public ObterAptidaoPorIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AptidaoDto?> Handle(ObterAptidaoPorIdQuery request, CancellationToken ct)
    {
        return await _db.AptidoesAtividadeEspecifica
            .Where(a => a.Id == request.Id)
            .Select(a => new AptidaoDto
            {
                Id = a.Id,
                TrabalhadorId = a.TrabalhadorId,
                AtividadeCritica = a.AtividadeCritica,
                Aptidao = a.Aptidao,
                DataAvaliacao = a.DataAvaliacao,
                DataValidade = a.DataValidade,
                MedicoResponsavel = a.MedicoResponsavel,
                Observacoes = a.Observacoes
            })
            .FirstOrDefaultAsync(ct);
    }
}
