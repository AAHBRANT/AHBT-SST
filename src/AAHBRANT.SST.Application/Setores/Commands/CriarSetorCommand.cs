using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Setores.Commands;

public record CriarSetorCommand(Guid ObraId, string Nome) : IRequest<Guid>;

public class CriarSetorCommandValidator : AbstractValidator<CriarSetorCommand>
{
    public CriarSetorCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
    }
}

public class CriarSetorCommandHandler : IRequestHandler<CriarSetorCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarSetorCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarSetorCommand request, CancellationToken ct)
    {
        var obraExiste = await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct);
        if (!obraExiste) throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var setor = new Setor
        {
            ObraId = request.ObraId,
            Nome = request.Nome
        };

        _db.Setores.Add(setor);
        await _db.SaveChangesAsync(ct);
        return setor.Id;
    }
}
