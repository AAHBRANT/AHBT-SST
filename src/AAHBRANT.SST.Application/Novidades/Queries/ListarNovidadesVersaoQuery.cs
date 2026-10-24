using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Novidades.Queries;

// Lista completa para a tela de cadastro (Administração → Novidades) — sem filtro de usuário.
public record ListarNovidadesVersaoQuery : IRequest<List<NovidadeVersaoDto>>;

public class ListarNovidadesVersaoQueryHandler : IRequestHandler<ListarNovidadesVersaoQuery, List<NovidadeVersaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarNovidadesVersaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<NovidadeVersaoDto>> Handle(ListarNovidadesVersaoQuery request, CancellationToken ct)
    {
        var novidades = await _db.NovidadesVersao
            .Include(n => n.Itens)
            .OrderByDescending(n => n.DataPublicacao)
            .ToListAsync(ct);

        return novidades.Select(NovidadesMapper.Mapear).ToList();
    }
}
