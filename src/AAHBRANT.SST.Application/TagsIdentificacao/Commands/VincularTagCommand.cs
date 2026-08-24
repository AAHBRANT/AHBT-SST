using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao.Commands;

// NTAG.md §2 — status transita AVAILABLE -> BOUND ao vincular a uma entidade (AREA/ASSET/WORKER).
public record VincularTagCommand(Guid Id, TipoEntidadeVinculada EntidadeVinculadaTipo, Guid EntidadeVinculadaId) : IRequest;

public class VincularTagCommandValidator : AbstractValidator<VincularTagCommand>
{
    public VincularTagCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.EntidadeVinculadaId).NotEmpty();
    }
}

public class VincularTagCommandHandler : IRequestHandler<VincularTagCommand>
{
    private readonly IAppDbContext _db;

    public VincularTagCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(VincularTagCommand request, CancellationToken ct)
    {
        var tag = await _db.TagsIdentificacao.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Tag {request.Id} não encontrada.");

        if (tag.Status != StatusTag.Disponivel)
            throw new InvalidOperationException("Só é possível vincular uma tag com status Disponível.");

        tag.EntidadeVinculadaTipo = request.EntidadeVinculadaTipo;
        tag.EntidadeVinculadaId = request.EntidadeVinculadaId;
        tag.Status = StatusTag.Vinculada;

        await _db.SaveChangesAsync(ct);
    }
}
