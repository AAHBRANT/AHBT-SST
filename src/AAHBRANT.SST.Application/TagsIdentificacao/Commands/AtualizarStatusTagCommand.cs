using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao.Commands;

// Para Desativada/Perdida (marcação direta, sem passar por Vincular/Desvincular).
public record AtualizarStatusTagCommand(Guid Id, StatusTag Status) : IRequest;

public class AtualizarStatusTagCommandValidator : AbstractValidator<AtualizarStatusTagCommand>
{
    public AtualizarStatusTagCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class AtualizarStatusTagCommandHandler : IRequestHandler<AtualizarStatusTagCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarStatusTagCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarStatusTagCommand request, CancellationToken ct)
    {
        var tag = await _db.TagsIdentificacao.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Tag {request.Id} não encontrada.");

        tag.Status = request.Status;
        if (request.Status == StatusTag.Disponivel)
        {
            tag.EntidadeVinculadaTipo = null;
            tag.EntidadeVinculadaId = null;
        }

        await _db.SaveChangesAsync(ct);
    }
}
