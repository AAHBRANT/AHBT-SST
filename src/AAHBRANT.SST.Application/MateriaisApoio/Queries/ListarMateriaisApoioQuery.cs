using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MateriaisApoio.Queries;

public record ListarMateriaisApoioQuery(string? Categoria = null) : IRequest<List<MaterialApoioDto>>;

public class ListarMateriaisApoioQueryHandler : IRequestHandler<ListarMateriaisApoioQuery, List<MaterialApoioDto>>
{
    private readonly IAppDbContext _db;

    public ListarMateriaisApoioQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<MaterialApoioDto>> Handle(ListarMateriaisApoioQuery request, CancellationToken ct)
    {
        var query = _db.MateriaisApoio.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Categoria))
            query = query.Where(m => m.Categoria == request.Categoria);

        return await query
            .OrderBy(m => m.Categoria)
            .ThenByDescending(m => m.CreatedAtUtc)
            .Select(m => new MaterialApoioDto
            {
                Id = m.Id,
                Nome = m.Nome,
                Categoria = m.Categoria,
                NomeArquivo = m.NomeArquivo,
                ContentType = m.ContentType,
                TamanhoBytes = m.Conteudo.Length,
                CreatedAtUtc = m.CreatedAtUtc,
            })
            .ToListAsync(ct);
    }
}
