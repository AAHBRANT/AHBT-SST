using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Atividades.Commands;

public record ExcluirAtividadeCommand(Guid Id) : IRequest;

public class ExcluirAtividadeCommandValidator : AbstractValidator<ExcluirAtividadeCommand>
{
    public ExcluirAtividadeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirAtividadeCommandHandler : IRequestHandler<ExcluirAtividadeCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirAtividadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirAtividadeCommand request, CancellationToken ct)
    {
        var atividade = await _db.Atividades.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Atividade {request.Id} não encontrada.");

        _db.Atividades.Remove(atividade);
        await _db.SaveChangesAsync(ct);
    }
}
