using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesEpi.Queries;

// Histórico de movimentações de um EPI numa Obra específica. Se ainda não existe linha de
// EstoqueEpi para o par (CatalogoEpiId, ObraId), não há movimentações — retorna lista vazia em
// vez de erro, já que "estoque zerado, nunca movimentado" é um estado válido.
public record ListarMovimentacoesEstoqueEpiQuery(Guid CatalogoEpiId, Guid ObraId) : IRequest<List<MovimentacaoEstoqueEpiDto>>;

public class ListarMovimentacoesEstoqueEpiQueryHandler : IRequestHandler<ListarMovimentacoesEstoqueEpiQuery, List<MovimentacaoEstoqueEpiDto>>
{
    private readonly IAppDbContext _db;
    public ListarMovimentacoesEstoqueEpiQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<MovimentacaoEstoqueEpiDto>> Handle(ListarMovimentacoesEstoqueEpiQuery request, CancellationToken ct)
        => await _db.MovimentacoesEstoqueEpi
            .Where(m => m.EstoqueEpi!.CatalogoEpiId == request.CatalogoEpiId && m.EstoqueEpi!.ObraId == request.ObraId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new MovimentacaoEstoqueEpiDto(m.Id, m.Tipo, m.Quantidade, m.SaldoResultante, m.CreatedAtUtc, m.Observacao, m.EntregaEpiId))
            .ToListAsync(ct);
}
