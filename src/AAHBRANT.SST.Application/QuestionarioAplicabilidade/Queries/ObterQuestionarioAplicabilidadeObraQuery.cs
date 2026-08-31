using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.QuestionarioAplicabilidade.Queries;

// Todos os itens do catálogo global, com a resposta desta obra quando existir (left join em
// memória) — a tela de questionário por obra precisa mostrar também as perguntas ainda não
// respondidas, não só as já respondidas.
public record ObterQuestionarioAplicabilidadeObraQuery(Guid ObraId) : IRequest<List<RespostaQuestionarioObraDto>>;

public class ObterQuestionarioAplicabilidadeObraQueryHandler
    : IRequestHandler<ObterQuestionarioAplicabilidadeObraQuery, List<RespostaQuestionarioObraDto>>
{
    private readonly IAppDbContext _db;

    public ObterQuestionarioAplicabilidadeObraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<RespostaQuestionarioObraDto>> Handle(ObterQuestionarioAplicabilidadeObraQuery request, CancellationToken ct)
    {
        var itens = await _db.ItensQuestionarioAplicabilidade.OrderBy(i => i.Pergunta).ToListAsync(ct);
        var respostas = await _db.RespostasQuestionarioAplicabilidade
            .Where(r => r.ObraId == request.ObraId)
            .ToDictionaryAsync(r => r.ItemQuestionarioAplicabilidadeId, r => r, ct);

        return itens.Select(i =>
        {
            respostas.TryGetValue(i.Id, out var resposta);
            return new RespostaQuestionarioObraDto(i.Id, i.Pergunta, i.TextoApoio, resposta?.Resposta, resposta?.Observacao);
        }).ToList();
    }
}
