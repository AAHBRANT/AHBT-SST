using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesEpc.Queries;

public record ListarMovimentacoesEstoqueEpcQuery(Guid CatalogoEpcId, Guid ObraId) : IRequest<List<MovimentacaoEstoqueEpcDto>>;

public class ListarMovimentacoesEstoqueEpcQueryHandler : IRequestHandler<ListarMovimentacoesEstoqueEpcQuery, List<MovimentacaoEstoqueEpcDto>>
{
    private readonly IAppDbContext _db;
    public ListarMovimentacoesEstoqueEpcQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<MovimentacaoEstoqueEpcDto>> Handle(ListarMovimentacoesEstoqueEpcQuery request, CancellationToken ct)
        => await _db.MovimentacoesEstoqueEpc
            .Where(m => m.EstoqueEpc!.CatalogoEpcId == request.CatalogoEpcId && m.EstoqueEpc!.ObraId == request.ObraId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new MovimentacaoEstoqueEpcDto(m.Id, m.Tipo, m.Quantidade, m.SaldoResultante, m.CreatedAtUtc, m.Observacao, m.InstalacaoEpcId))
            .ToListAsync(ct);
}
