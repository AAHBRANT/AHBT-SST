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

        var lista = await _db.PgrRevisoes
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
                CriadoPorId = r.CreatedBy,
            })
            .ToListAsync(ct);
        // Nome em consulta separada (IgnoreQueryFilters na mesma consulta desligaria o filtro de Ativo).
        var nomes = await Common.NomesPorId.UsuariosAsync(_db, lista.Select(r => r.CriadoPorId), ct);
        foreach (var r in lista)
            r.CriadoPorNome = r.CriadoPorId is { } u && nomes.TryGetValue(u, out var n) ? n : null;
        return lista;
    }
}
