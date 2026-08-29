using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.QuestionarioAplicabilidade.Commands;

public record CriarItemQuestionarioAplicabilidadeCommand(string Pergunta, string? TextoApoio) : IRequest<Guid>;

public class CriarItemQuestionarioAplicabilidadeCommandValidator : AbstractValidator<CriarItemQuestionarioAplicabilidadeCommand>
{
    public CriarItemQuestionarioAplicabilidadeCommandValidator()
    {
        RuleFor(x => x.Pergunta).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TextoApoio).MaximumLength(500);
    }
}

public class CriarItemQuestionarioAplicabilidadeCommandHandler : IRequestHandler<CriarItemQuestionarioAplicabilidadeCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarItemQuestionarioAplicabilidadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarItemQuestionarioAplicabilidadeCommand request, CancellationToken ct)
    {
        var item = new ItemQuestionarioAplicabilidade
        {
            Pergunta = request.Pergunta,
            TextoApoio = request.TextoApoio,
        };

        _db.ItensQuestionarioAplicabilidade.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }
}
