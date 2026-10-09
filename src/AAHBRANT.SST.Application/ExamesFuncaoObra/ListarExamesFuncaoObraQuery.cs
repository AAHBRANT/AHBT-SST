using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.ExamesFuncaoObra;

public record ExameFuncaoObraDto(
    Guid Id, Guid FuncaoId, string? FuncaoNome, Guid? PcmsoDetalheId, string Exame, string? CodigoExame,
    bool Admissional, bool Periodico, bool RetornoTrabalho, bool MudancaRisco, bool Demissional,
    int? PeriodicidadeMeses, string? Observacao);

// Quadro de exames do PCMSO da obra. Com FuncaoId, só os exames daquela função (perfil do trabalhador).
public record ListarExamesFuncaoObraQuery(Guid ObraId, Guid? FuncaoId) : IRequest<List<ExameFuncaoObraDto>>;

public class ListarExamesFuncaoObraQueryHandler : IRequestHandler<ListarExamesFuncaoObraQuery, List<ExameFuncaoObraDto>>
{
    private readonly IAppDbContext _db;

    public ListarExamesFuncaoObraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ExameFuncaoObraDto>> Handle(ListarExamesFuncaoObraQuery request, CancellationToken ct)
    {
        var query = _db.ExamesFuncaoObra.AsNoTracking().Where(e => e.ObraId == request.ObraId);
        if (request.FuncaoId is { } funcaoId)
            query = query.Where(e => e.FuncaoId == funcaoId);

        var itens = await query
            .Select(e => new ExameFuncaoObraDto(
                e.Id, e.FuncaoId,
                _db.Funcoes.IgnoreQueryFilters().Where(f => f.Id == e.FuncaoId).Select(f => f.Nome).FirstOrDefault(),
                e.PcmsoDetalheId, e.Exame, e.CodigoExame, e.Admissional, e.Periodico, e.RetornoTrabalho,
                e.MudancaRisco, e.Demissional, e.PeriodicidadeMeses, e.Observacao))
            .ToListAsync(ct);

        return itens.OrderBy(e => e.FuncaoNome).ThenBy(e => e.Exame).ToList();
    }
}
