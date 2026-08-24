using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GestaoDocumental.Commands;

// Reclassificação direta de status (mesmo padrão de AtualizarStatusRequisitoLegalCommand), sem
// bloqueio preventivo sequencial: embora o §31 liste o fluxo "Rascunho → Em aprovação → Vigente →
// Obsoleto → Cancelado" em ordem, um documento controlado real pode ser cancelado a partir de
// qualquer estado (ex.: revogação de uma minuta em aprovação) — decisão própria, a confirmar com o
// usuário se ele preferir um fluxo linear com bloqueio (como PT/APR/NC/Acidente).
public record AtualizarStatusDocumentoGestaoCommand(Guid Id, StatusDocumentoGestao NovoStatus) : IRequest;

public class AtualizarStatusDocumentoGestaoCommandValidator : AbstractValidator<AtualizarStatusDocumentoGestaoCommand>
{
    public AtualizarStatusDocumentoGestaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class AtualizarStatusDocumentoGestaoCommandHandler : IRequestHandler<AtualizarStatusDocumentoGestaoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarStatusDocumentoGestaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarStatusDocumentoGestaoCommand request, CancellationToken ct)
    {
        var documento = await _db.DocumentosGestao.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Documento {request.Id} não encontrado.");

        documento.Status = request.NovoStatus;

        await _db.SaveChangesAsync(ct);
    }
}
