using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Commands;

public record ExcluirEntregaEpiCommand(Guid Id) : IRequest;

public class ExcluirEntregaEpiCommandValidator : AbstractValidator<ExcluirEntregaEpiCommand>
{
    public ExcluirEntregaEpiCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirEntregaEpiCommandHandler : IRequestHandler<ExcluirEntregaEpiCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirEntregaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirEntregaEpiCommand request, CancellationToken ct)
    {
        var entrega = await _db.EntregasEpi.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Entrega de EPI não encontrada.");

        _db.EntregasEpi.Remove(entrega);
        await _db.SaveChangesAsync(ct);
    }
}
