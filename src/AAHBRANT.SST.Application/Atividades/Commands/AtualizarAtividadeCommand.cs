using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Atividades.Commands;

public record AtualizarAtividadeCommand(Guid Id, Guid ObraId, string Nome, string? Descricao) : IRequest;

public class AtualizarAtividadeCommandValidator : AbstractValidator<AtualizarAtividadeCommand>
{
    public AtualizarAtividadeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
    }
}

public class AtualizarAtividadeCommandHandler : IRequestHandler<AtualizarAtividadeCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarAtividadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarAtividadeCommand request, CancellationToken ct)
    {
        var atividade = await _db.Atividades.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Atividade {request.Id} não encontrada.");

        atividade.ObraId = request.ObraId;
        atividade.Nome = request.Nome;
        atividade.Descricao = request.Descricao;

        await _db.SaveChangesAsync(ct);
    }
}
