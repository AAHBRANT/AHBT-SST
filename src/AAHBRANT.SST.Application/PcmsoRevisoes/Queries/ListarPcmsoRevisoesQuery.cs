using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pcmso;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PcmsoRevisoes.Queries;

public record ListarPcmsoRevisoesQuery(Guid PcmsoId) : IRequest<List<PcmsoRevisaoDto>>;

public class ListarPcmsoRevisoesQueryHandler : IRequestHandler<ListarPcmsoRevisoesQuery, List<PcmsoRevisaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPcmsoRevisoesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PcmsoRevisaoDto>> Handle(ListarPcmsoRevisoesQuery request, CancellationToken ct)
    {
        var revisoes = await _db.PcmsoRevisoes
            .Where(r => r.PcmsoId == request.PcmsoId)
            .OrderByDescending(r => r.NumeroRevisao)
            .ToListAsync(ct);

        return revisoes.Select(r => new PcmsoRevisaoDto
        {
            Id = r.Id,
            PcmsoId = r.PcmsoId,
            NumeroRevisao = r.NumeroRevisao,
            DataRevisao = r.DataRevisao,
            Motivo = r.Motivo,
            ResponsavelUsuarioId = r.ResponsavelUsuarioId,
        }).ToList();
    }
}
