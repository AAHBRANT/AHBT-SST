using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmsos.Commands;

public record ExcluirPcmsoCommand(Guid Id) : IRequest;

public class ExcluirPcmsoCommandValidator : AbstractValidator<ExcluirPcmsoCommand>
{
    public ExcluirPcmsoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirPcmsoCommandHandler : IRequestHandler<ExcluirPcmsoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirPcmsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirPcmsoCommand request, CancellationToken ct)
    {
        var documento = await _db.DocumentosGestao.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"PCMSO {request.Id} não encontrado.");

        // Soft-delete não segue ON DELETE CASCADE físico (nunca chega a rodar DELETE de verdade —
        // AplicarAuditoria intercepta e vira UPDATE Ativo=false), então o detalhe precisa ser
        // desativado explicitamente junto do documento genérico.
        var detalhe = await _db.PcmsoDetalhes.FirstOrDefaultAsync(p => p.DocumentoGestaoId == request.Id, ct);
        if (detalhe is not null)
            _db.PcmsoDetalhes.Remove(detalhe);

        _db.DocumentosGestao.Remove(documento);
        await _db.SaveChangesAsync(ct);
    }
}
