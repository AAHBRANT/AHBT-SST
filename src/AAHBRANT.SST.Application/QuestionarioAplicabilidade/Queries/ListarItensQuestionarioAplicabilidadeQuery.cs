using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.QuestionarioAplicabilidade.Queries;

public record ListarItensQuestionarioAplicabilidadeQuery : IRequest<List<ItemQuestionarioAplicabilidadeDto>>;

public class ListarItensQuestionarioAplicabilidadeQueryHandler
    : IRequestHandler<ListarItensQuestionarioAplicabilidadeQuery, List<ItemQuestionarioAplicabilidadeDto>>
{
    private readonly IAppDbContext _db;

    public ListarItensQuestionarioAplicabilidadeQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ItemQuestionarioAplicabilidadeDto>> Handle(ListarItensQuestionarioAplicabilidadeQuery request, CancellationToken ct)
        => await _db.ItensQuestionarioAplicabilidade
            .OrderBy(i => i.Pergunta)
            .Select(i => new ItemQuestionarioAplicabilidadeDto(i.Id, i.Pergunta, i.TextoApoio))
            .ToListAsync(ct);
}
