using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.QuestionarioAplicabilidade.Commands;

// Upsert: responder de novo o mesmo item da mesma obra atualiza a linha existente em vez de
// duplicar (índice único ObraId+ItemId) — a obra pode revisar uma resposta anterior (mudou o tipo
// de atividade, por exemplo) sem acumular histórico de respostas.
public record ResponderQuestionarioAplicabilidadeCommand(
    Guid ObraId,
    Guid ItemQuestionarioAplicabilidadeId,
    bool Resposta,
    string? Observacao) : IRequest;

public class ResponderQuestionarioAplicabilidadeCommandValidator : AbstractValidator<ResponderQuestionarioAplicabilidadeCommand>
{
    public ResponderQuestionarioAplicabilidadeCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.ItemQuestionarioAplicabilidadeId).NotEmpty();
        RuleFor(x => x.Observacao).MaximumLength(500);
    }
}

public class ResponderQuestionarioAplicabilidadeCommandHandler : IRequestHandler<ResponderQuestionarioAplicabilidadeCommand>
{
    private readonly IAppDbContext _db;

    public ResponderQuestionarioAplicabilidadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ResponderQuestionarioAplicabilidadeCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");
        if (!await _db.ItensQuestionarioAplicabilidade.AnyAsync(i => i.Id == request.ItemQuestionarioAplicabilidadeId, ct))
            throw new KeyNotFoundException($"Item de questionário {request.ItemQuestionarioAplicabilidadeId} não encontrado.");

        var resposta = await _db.RespostasQuestionarioAplicabilidade.FirstOrDefaultAsync(
            r => r.ObraId == request.ObraId && r.ItemQuestionarioAplicabilidadeId == request.ItemQuestionarioAplicabilidadeId, ct);

        if (resposta is null)
        {
            resposta = new RespostaQuestionarioAplicabilidade
            {
                ObraId = request.ObraId,
                ItemQuestionarioAplicabilidadeId = request.ItemQuestionarioAplicabilidadeId,
            };
            _db.RespostasQuestionarioAplicabilidade.Add(resposta);
        }

        resposta.Resposta = request.Resposta;
        resposta.Observacao = request.Observacao;

        await _db.SaveChangesAsync(ct);
    }
}
