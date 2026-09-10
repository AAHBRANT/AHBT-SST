using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesUniforme.Queries;

public record ListarMovimentacoesEstoqueUniformeQuery(Guid CatalogoUniformeId, Guid ObraId, string Tamanho)
    : IRequest<List<MovimentacaoEstoqueUniformeDto>>;

public class ListarMovimentacoesEstoqueUniformeQueryHandler
    : IRequestHandler<ListarMovimentacoesEstoqueUniformeQuery, List<MovimentacaoEstoqueUniformeDto>>
{
    private readonly IAppDbContext _db;
    public ListarMovimentacoesEstoqueUniformeQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<MovimentacaoEstoqueUniformeDto>> Handle(ListarMovimentacoesEstoqueUniformeQuery request, CancellationToken ct)
        => await _db.MovimentacoesEstoqueUniforme
            .Where(m => m.EstoqueUniforme!.CatalogoUniformeId == request.CatalogoUniformeId
                && m.EstoqueUniforme!.ObraId == request.ObraId
                && m.EstoqueUniforme!.Tamanho == request.Tamanho)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new MovimentacaoEstoqueUniformeDto(m.Id, m.Tipo, m.Quantidade, m.SaldoResultante, m.CreatedAtUtc, m.Observacao, m.EntregaUniformeId))
            .ToListAsync(ct);
}
