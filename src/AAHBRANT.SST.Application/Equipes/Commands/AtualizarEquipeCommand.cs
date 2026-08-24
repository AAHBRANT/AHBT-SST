using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Equipes.Commands;

public record AtualizarEquipeCommand(Guid Id, Guid SetorId, string Nome, Guid? EncarregadoId) : IRequest;

public class AtualizarEquipeCommandValidator : AbstractValidator<AtualizarEquipeCommand>
{
    public AtualizarEquipeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SetorId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
    }
}

public class AtualizarEquipeCommandHandler : IRequestHandler<AtualizarEquipeCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarEquipeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarEquipeCommand request, CancellationToken ct)
    {
        var equipe = await _db.Equipes.FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Equipe {request.Id} não encontrada.");

        var setorExiste = await _db.Setores.AnyAsync(s => s.Id == request.SetorId, ct);
        if (!setorExiste) throw new KeyNotFoundException($"Setor {request.SetorId} não encontrado.");

        if (request.EncarregadoId.HasValue)
        {
            var encarregadoExiste = await _db.Trabalhadores.AnyAsync(t => t.Id == request.EncarregadoId.Value, ct);
            if (!encarregadoExiste) throw new KeyNotFoundException($"Trabalhador {request.EncarregadoId} não encontrado.");
        }

        equipe.SetorId = request.SetorId;
        equipe.Nome = request.Nome;
        equipe.EncarregadoId = request.EncarregadoId;

        await _db.SaveChangesAsync(ct);
    }
}
