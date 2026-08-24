using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Equipes.Commands;

public record CriarEquipeCommand(Guid SetorId, string Nome, Guid? EncarregadoId) : IRequest<Guid>;

public class CriarEquipeCommandValidator : AbstractValidator<CriarEquipeCommand>
{
    public CriarEquipeCommandValidator()
    {
        RuleFor(x => x.SetorId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
    }
}

public class CriarEquipeCommandHandler : IRequestHandler<CriarEquipeCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarEquipeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarEquipeCommand request, CancellationToken ct)
    {
        var setorExiste = await _db.Setores.AnyAsync(s => s.Id == request.SetorId, ct);
        if (!setorExiste) throw new KeyNotFoundException($"Setor {request.SetorId} não encontrado.");

        if (request.EncarregadoId.HasValue)
        {
            var encarregadoExiste = await _db.Trabalhadores.AnyAsync(t => t.Id == request.EncarregadoId.Value, ct);
            if (!encarregadoExiste) throw new KeyNotFoundException($"Trabalhador {request.EncarregadoId} não encontrado.");
        }

        var equipe = new Equipe
        {
            SetorId = request.SetorId,
            Nome = request.Nome,
            EncarregadoId = request.EncarregadoId
        };

        _db.Equipes.Add(equipe);
        await _db.SaveChangesAsync(ct);
        return equipe.Id;
    }
}
