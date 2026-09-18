using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Commands;

public record ConfirmarEntregaEpiCommand(Guid EntregaEpiId) : IRequest;

public class ConfirmarEntregaEpiCommandValidator : AbstractValidator<ConfirmarEntregaEpiCommand>
{
    public ConfirmarEntregaEpiCommandValidator()
    {
        RuleFor(x => x.EntregaEpiId).NotEmpty();
    }
}

public class ConfirmarEntregaEpiCommandHandler : IRequestHandler<ConfirmarEntregaEpiCommand>
{
    private readonly IAppDbContext _db;
    public ConfirmarEntregaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ConfirmarEntregaEpiCommand request, CancellationToken ct)
    {
        var entrega = await _db.EntregasEpi.FirstOrDefaultAsync(x => x.Id == request.EntregaEpiId, ct)
            ?? throw new KeyNotFoundException("Entrega de EPI não encontrada.");

        if (entrega.Confirmada)
            throw new InvalidOperationException("Esta entrega já está confirmada.");

        entrega.Confirmada = true;
        entrega.DataConfirmacao = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
