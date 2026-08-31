using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.QuestionarioAplicabilidade.Commands;

public record ExcluirItemQuestionarioAplicabilidadeCommand(Guid Id) : IRequest;

public class ExcluirItemQuestionarioAplicabilidadeCommandValidator : AbstractValidator<ExcluirItemQuestionarioAplicabilidadeCommand>
{
    public ExcluirItemQuestionarioAplicabilidadeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirItemQuestionarioAplicabilidadeCommandHandler : IRequestHandler<ExcluirItemQuestionarioAplicabilidadeCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirItemQuestionarioAplicabilidadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirItemQuestionarioAplicabilidadeCommand request, CancellationToken ct)
    {
        var item = await _db.ItensQuestionarioAplicabilidade.FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Item de questionário {request.Id} não encontrado.");

        _db.ItensQuestionarioAplicabilidade.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}
