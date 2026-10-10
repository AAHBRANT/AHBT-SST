using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pgrs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PgrRevisoes.Queries;

public record ListarPgrRevisoesQuery(Guid PgrId) : IRequest<List<PgrRevisaoDto>>;

public class ListarPgrRevisoesQueryHandler : IRequestHandler<ListarPgrRevisoesQuery, List<PgrRevisaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPgrRevisoesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PgrRevisaoDto>> Handle(ListarPgrRevisoesQuery request, CancellationToken ct)
    {
        // Pgrs tem filtro por obra; PgrRevisoes não. Projeção explícita para não carregar os PDFs.
        if (!await _db.Pgrs.AnyAsync(p => p.Id == request.PgrId, ct))
            return new List<PgrRevisaoDto>();

        return await _db.PgrRevisoes
            .Where(r => r.PgrId == request.PgrId)
            .OrderByDescending(r => r.NumeroRevisao)
            .Select(r => new PgrRevisaoDto
            {
                Id = r.Id,
                PgrId = r.PgrId,
                NumeroRevisao = r.NumeroRevisao,
                DataRevisao = r.DataRevisao,
                Motivo = r.Motivo,
                ResponsavelUsuarioId = r.ResponsavelUsuarioId,
                TemDocumento = r.DocumentoConteudo != null,
                DocumentoNomeArquivo = r.DocumentoNomeArquivo,
                CriadoEmUtc = r.CreatedAtUtc,
                CriadoPorNome = _db.Usuarios.IgnoreQueryFilters().Where(u => u.Id == r.CreatedBy).Select(u => u.Nome).FirstOrDefault(),
            })
            .ToListAsync(ct);
    }
}
