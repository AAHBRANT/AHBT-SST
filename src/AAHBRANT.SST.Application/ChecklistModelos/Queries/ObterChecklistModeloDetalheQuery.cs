using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.ChecklistModelos.Queries;

public record ObterChecklistModeloDetalheQuery(Guid Id) : IRequest<ChecklistModeloDetalheDto?>;

public class ObterChecklistModeloDetalheQueryHandler : IRequestHandler<ObterChecklistModeloDetalheQuery, ChecklistModeloDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterChecklistModeloDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ChecklistModeloDetalheDto?> Handle(ObterChecklistModeloDetalheQuery request, CancellationToken ct)
    {
        var checklist = await _db.ChecklistModelos.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (checklist is null) return null;

        var itens = await _db.ChecklistModeloItens
            .Where(i => i.ChecklistModeloId == checklist.Id)
            .OrderBy(i => i.Ordem)
            .ToListAsync(ct);

        return new ChecklistModeloDetalheDto
        {
            ChecklistModelo = new ChecklistModeloDto
            {
                Id = checklist.Id,
                Nome = checklist.Nome,
                TipoInspecao = checklist.TipoInspecao,
                Versao = checklist.Versao,
                ChecklistModeloAnteriorId = checklist.ChecklistModeloAnteriorId,
                QuantidadeItens = itens.Count
            },
            Itens = itens.Select(i => new ChecklistModeloItemDto
            {
                Id = i.Id,
                ChecklistModeloId = i.ChecklistModeloId,
                Ordem = i.Ordem,
                Descricao = i.Descricao,
                ExigeFotografia = i.ExigeFotografia,
                ExigeResponsavel = i.ExigeResponsavel,
                ExigePrazo = i.ExigePrazo
            }).ToList()
        };
    }
}
