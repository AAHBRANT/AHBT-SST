using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Setores.Queries;

public record ListarSetoresQuery(Guid? ObraId) : IRequest<List<SetorDto>>;

public class ListarSetoresQueryHandler : IRequestHandler<ListarSetoresQuery, List<SetorDto>>
{
    private readonly IAppDbContext _db;

    public ListarSetoresQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<SetorDto>> Handle(ListarSetoresQuery request, CancellationToken ct)
    {
        var query = _db.Setores.AsQueryable();
        if (request.ObraId.HasValue)
        {
            query = query.Where(s => s.ObraId == request.ObraId.Value);
        }

        // O nome da obra vem de consulta separada, e não de `s.Obra!.Nome`: projetar navegação
        // obrigatória gera INNER JOIN, e Obra tem filtro global por Ativo. Com "excluir" sendo soft
        // delete neste sistema, uma obra excluída fazia TODOS os setores dela sumirem da lista —
        // junto com as equipes e os trabalhadores pendurados neles, que continuam no banco e ficam
        // sem tela para corrigir. Mesma armadilha que derrubou a entrega de EPI em 23/09.
        var setores = await query
            .Select(s => new { s.Id, s.ObraId, s.Nome })
            .ToListAsync(ct);

        var obraIds = setores.Select(s => s.ObraId).Distinct().ToList();
        var nomeObraPorId = (await _db.Obras.AsNoTracking().IgnoreQueryFilters()
                .Where(o => obraIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Nome, o.Ativo })
                .ToListAsync(ct))
            // Obra excluída aparece com o nome real, para a pessoa reconhecer de onde é o setor —
            // marcada como removida, para a tela não afirmar que a obra continua lá.
            .ToDictionary(o => o.Id, o => o.Ativo ? o.Nome : $"{o.Nome} (obra removida)");

        return setores
            .Select(s => new SetorDto
            {
                Id = s.Id,
                ObraId = s.ObraId,
                ObraNome = nomeObraPorId.GetValueOrDefault(s.ObraId, "Obra removida"),
                Nome = s.Nome
            })
            .OrderBy(s => s.Nome)
            .ToList();
    }
}
