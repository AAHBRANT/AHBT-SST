using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Setores.Commands;

public record AtualizarSetorCommand(Guid Id, Guid ObraId, string Nome) : IRequest;

public class AtualizarSetorCommandValidator : AbstractValidator<AtualizarSetorCommand>
{
    public AtualizarSetorCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
    }
}

public class AtualizarSetorCommandHandler : IRequestHandler<AtualizarSetorCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarSetorCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarSetorCommand request, CancellationToken ct)
    {
        var setor = await _db.Setores.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Setor {request.Id} não encontrado.");

        var obraExiste = await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct);
        if (!obraExiste) throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        setor.ObraId = request.ObraId;
        setor.Nome = request.Nome;

        await _db.SaveChangesAsync(ct);
    }
}
