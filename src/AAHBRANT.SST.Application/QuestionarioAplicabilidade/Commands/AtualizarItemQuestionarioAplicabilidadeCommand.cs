using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.QuestionarioAplicabilidade.Commands;

public record AtualizarItemQuestionarioAplicabilidadeCommand(Guid Id, string Pergunta, string? TextoApoio) : IRequest;

public class AtualizarItemQuestionarioAplicabilidadeCommandValidator : AbstractValidator<AtualizarItemQuestionarioAplicabilidadeCommand>
{
    public AtualizarItemQuestionarioAplicabilidadeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Pergunta).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TextoApoio).MaximumLength(500);
    }
}

public class AtualizarItemQuestionarioAplicabilidadeCommandHandler : IRequestHandler<AtualizarItemQuestionarioAplicabilidadeCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarItemQuestionarioAplicabilidadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarItemQuestionarioAplicabilidadeCommand request, CancellationToken ct)
    {
        var item = await _db.ItensQuestionarioAplicabilidade.FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Item de questionário {request.Id} não encontrado.");

        item.Pergunta = request.Pergunta;
        item.TextoApoio = request.TextoApoio;

        await _db.SaveChangesAsync(ct);
    }
}
