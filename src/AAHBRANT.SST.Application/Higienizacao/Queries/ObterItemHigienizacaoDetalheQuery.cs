using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Higienizacao.Queries;

public record ObterItemHigienizacaoDetalheQuery(Guid Id) : IRequest<ItemHigienizacaoDetalheDto?>;

public class ObterItemHigienizacaoDetalheQueryHandler : IRequestHandler<ObterItemHigienizacaoDetalheQuery, ItemHigienizacaoDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterItemHigienizacaoDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ItemHigienizacaoDetalheDto?> Handle(ObterItemHigienizacaoDetalheQuery request, CancellationToken ct)
    {
        var item = await _db.ItensHigienizacao
            .Include(i => i.Obra)
            .Include(i => i.Registros).ThenInclude(r => r.Trabalhador)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct);

        if (item is null) return null;

        return new ItemHigienizacaoDetalheDto
        {
            Item = ListarItensHigienizacaoQueryHandler.MapearParaDto(item),
            Registros = item.Registros
                .Where(r => r.Ativo)
                .OrderByDescending(r => r.DataHora)
                .Select(r => new RegistroHigienizacaoDto
                {
                    Id = r.Id,
                    TrabalhadorId = r.TrabalhadorId,
                    TrabalhadorNome = r.Trabalhador?.Nome ?? string.Empty,
                    DataHora = r.DataHora,
                    Observacoes = r.Observacoes,
                })
                .ToList(),
        };
    }
}
