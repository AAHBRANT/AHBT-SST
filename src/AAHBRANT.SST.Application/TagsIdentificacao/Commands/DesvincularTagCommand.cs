using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao.Commands;

public record DesvincularTagCommand(Guid Id) : IRequest;

public class DesvincularTagCommandValidator : AbstractValidator<DesvincularTagCommand>
{
    public DesvincularTagCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DesvincularTagCommandHandler : IRequestHandler<DesvincularTagCommand>
{
    private readonly IAppDbContext _db;

    public DesvincularTagCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DesvincularTagCommand request, CancellationToken ct)
    {
        var tag = await _db.TagsIdentificacao.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Tag {request.Id} não encontrada.");

        tag.EntidadeVinculadaTipo = null;
        tag.EntidadeVinculadaId = null;
        tag.Status = StatusTag.Disponivel;

        await _db.SaveChangesAsync(ct);
    }
}
