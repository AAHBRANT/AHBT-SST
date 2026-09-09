using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Limpar um quadro da grade de evidências (04/09, pedido do usuário) — mesmo padrão de
// RemoverFotoEvidenciaDdsCommand.
public record RemoverFotoEvidenciaSessaoTreinamentoCommand(Guid FotoId) : IRequest;

public class RemoverFotoEvidenciaSessaoTreinamentoCommandValidator : AbstractValidator<RemoverFotoEvidenciaSessaoTreinamentoCommand>
{
    public RemoverFotoEvidenciaSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.FotoId).NotEmpty();
    }
}

public class RemoverFotoEvidenciaSessaoTreinamentoCommandHandler : IRequestHandler<RemoverFotoEvidenciaSessaoTreinamentoCommand>
{
    private readonly IAppDbContext _db;
    public RemoverFotoEvidenciaSessaoTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RemoverFotoEvidenciaSessaoTreinamentoCommand request, CancellationToken ct)
    {
        var foto = await _db.FotosEvidenciaSessaoTreinamento.FirstOrDefaultAsync(f => f.Id == request.FotoId, ct)
            ?? throw new KeyNotFoundException($"Foto de evidência {request.FotoId} não encontrada.");

        _db.FotosEvidenciaSessaoTreinamento.Remove(foto);
        await _db.SaveChangesAsync(ct);
    }
}
