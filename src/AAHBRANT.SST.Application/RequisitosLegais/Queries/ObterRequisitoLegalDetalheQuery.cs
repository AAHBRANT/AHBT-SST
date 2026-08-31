using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RequisitosLegais.Queries;

public record ObterRequisitoLegalDetalheQuery(Guid Id) : IRequest<RequisitoLegalDetalheDto?>;

public class ObterRequisitoLegalDetalheQueryHandler : IRequestHandler<ObterRequisitoLegalDetalheQuery, RequisitoLegalDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterRequisitoLegalDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<RequisitoLegalDetalheDto?> Handle(ObterRequisitoLegalDetalheQuery request, CancellationToken ct)
    {
        var requisito = await _db.RequisitosLegais
            .Where(r => r.Id == request.Id)
            .Select(r => new RequisitoLegalDto(r.Id, r.Norma, r.Artigo, r.Titulo, r.Descricao, r.Categoria, r.Status, r.Fonte))
            .FirstOrDefaultAsync(ct);
        if (requisito is null) return null;

        var criterios = await _db.RequisitoLegalCriterios
            .Where(c => c.RequisitoLegalId == request.Id)
            .Include(c => c.Perigo)
            .Include(c => c.Funcao)
            .Include(c => c.ItemQuestionarioAplicabilidade)
            .Select(c => new RequisitoLegalCriterioDto(
                c.Id, c.Tipo,
                c.PerigoId, c.Perigo!.Nome,
                c.FuncaoId, c.Funcao!.Nome,
                c.TipoEquipamento,
                c.ItemQuestionarioAplicabilidadeId, c.ItemQuestionarioAplicabilidade!.Pergunta))
            .ToListAsync(ct);

        return new RequisitoLegalDetalheDto(requisito, criterios);
    }
}
