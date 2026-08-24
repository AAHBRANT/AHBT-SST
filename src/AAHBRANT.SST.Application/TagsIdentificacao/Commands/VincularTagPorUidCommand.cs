using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao.Commands;

// Fluxo real de campo: o operador lê o Uid físico da tag (NFC/QR) e vincula direto por ele, sem
// precisar antes descobrir o Id (Guid) interno da tag numa listagem — o que VincularTagCommand exige.
public record VincularTagPorUidCommand(string Uid, TipoEntidadeVinculada EntidadeVinculadaTipo, Guid EntidadeVinculadaId) : IRequest;

public class VincularTagPorUidCommandValidator : AbstractValidator<VincularTagPorUidCommand>
{
    public VincularTagPorUidCommandValidator()
    {
        RuleFor(x => x.Uid).NotEmpty();
        RuleFor(x => x.EntidadeVinculadaId).NotEmpty();
    }
}

public class VincularTagPorUidCommandHandler : IRequestHandler<VincularTagPorUidCommand>
{
    private readonly IAppDbContext _db;

    public VincularTagPorUidCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(VincularTagPorUidCommand request, CancellationToken ct)
    {
        var tag = await _db.TagsIdentificacao.FirstOrDefaultAsync(t => t.Uid == request.Uid, ct)
            ?? throw new KeyNotFoundException($"Tag com Uid '{request.Uid}' não encontrada.");

        if (tag.Status != StatusTag.Disponivel)
            throw new InvalidOperationException("Só é possível vincular uma tag com status Disponível.");

        tag.EntidadeVinculadaTipo = request.EntidadeVinculadaTipo;
        tag.EntidadeVinculadaId = request.EntidadeVinculadaId;
        tag.Status = StatusTag.Vinculada;

        await _db.SaveChangesAsync(ct);
    }
}
