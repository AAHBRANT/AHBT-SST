using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

public record ListarTamanhosUniformeTrabalhadorQuery(Guid TrabalhadorId) : IRequest<List<TamanhoUniformeTrabalhadorDto>>;

public class ListarTamanhosUniformeTrabalhadorQueryHandler : IRequestHandler<ListarTamanhosUniformeTrabalhadorQuery, List<TamanhoUniformeTrabalhadorDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _usuarioAtual;
    public ListarTamanhosUniformeTrabalhadorQueryHandler(IAppDbContext db, ICurrentUserService usuarioAtual)
    {
        _db = db;
        _usuarioAtual = usuarioAtual;
    }

    public async Task<List<TamanhoUniformeTrabalhadorDto>> Handle(ListarTamanhosUniformeTrabalhadorQuery request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

        // Revalida a projeção sincronizada antes de responder, inclusive para dados antigos.
        await new DefinirTamanhosUniformeTrabalhadorCommandHandler(_db, _usuarioAtual)
            .SincronizarTamanhosGrhAsync(trabalhador, ct);

        return await _db.TrabalhadorTamanhosUniforme
            .Where(t => t.TrabalhadorId == request.TrabalhadorId)
            .OrderBy(t => t.CatalogoUniforme!.Nome)
            .Select(t => new TamanhoUniformeTrabalhadorDto(t.CatalogoUniformeId, t.CatalogoUniforme!.Nome, t.Tamanho))
            .ToListAsync(ct);
    }
}
