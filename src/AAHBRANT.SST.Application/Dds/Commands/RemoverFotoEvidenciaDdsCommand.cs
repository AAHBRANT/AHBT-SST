using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Limpar um quadro da grade de evidências (04/09, pedido do usuário) — botão de remover/substituir
// no slot preenchido. Soft-delete padrão (ver AuditableEntity); o slot volta a aparecer vazio.
public record RemoverFotoEvidenciaDdsCommand(Guid FotoId) : IRequest;

public class RemoverFotoEvidenciaDdsCommandValidator : AbstractValidator<RemoverFotoEvidenciaDdsCommand>
{
    public RemoverFotoEvidenciaDdsCommandValidator()
    {
        RuleFor(x => x.FotoId).NotEmpty();
    }
}

public class RemoverFotoEvidenciaDdsCommandHandler : IRequestHandler<RemoverFotoEvidenciaDdsCommand>
{
    private readonly IAppDbContext _db;
    public RemoverFotoEvidenciaDdsCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RemoverFotoEvidenciaDdsCommand request, CancellationToken ct)
    {
        var foto = await _db.DdsFotosEvidencia.FirstOrDefaultAsync(f => f.Id == request.FotoId, ct)
            ?? throw new KeyNotFoundException($"Foto de evidência {request.FotoId} não encontrada.");

        _db.DdsFotosEvidencia.Remove(foto);
        await _db.SaveChangesAsync(ct);
    }
}
