using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Queries;

// CorSugerida = SemPadrao (0) quando o nome da função não se encaixa no padrão AAHBRANT.
public record SugestaoCorCapaceteDto(Guid FuncaoId, string Nome, CorCapacete CorAtual, CorCapacete CorSugerida,
    // Grupo do padrão que casou (ex.: "Engenheiros, mestres, encarregados, gestores"); nulo quando não há sugestão.
    string? Grupo = null);

public record ListarSugestoesCorCapaceteQuery : IRequest<List<SugestaoCorCapaceteDto>>;

public class ListarSugestoesCorCapaceteQueryHandler : IRequestHandler<ListarSugestoesCorCapaceteQuery, List<SugestaoCorCapaceteDto>>
{
    private readonly IAppDbContext _db;

    public ListarSugestoesCorCapaceteQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<SugestaoCorCapaceteDto>> Handle(ListarSugestoesCorCapaceteQuery request, CancellationToken ct)
    {
        var funcoes = await _db.Funcoes.AsNoTracking()
            .OrderBy(f => f.Nome)
            .Select(f => new { f.Id, f.Nome, f.CorCapacete })
            .ToListAsync(ct);

        return funcoes
            .Select(f =>
            {
                var sugerida = SugestaoCorCapacete.Sugerir(f.Nome);
                return new SugestaoCorCapaceteDto(f.Id, f.Nome, f.CorCapacete, sugerida, SugestaoCorCapacete.Grupo(sugerida));
            })
            .ToList();
    }
}
