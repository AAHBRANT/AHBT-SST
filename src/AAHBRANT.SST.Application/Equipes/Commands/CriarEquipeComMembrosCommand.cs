using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Equipes.Commands;

// Equipe montada dentro da APR (24/09/2026): o usuário marca os responsáveis e salva a seleção
// como equipe para reusar. A tela de Equipes/Setores saiu da navegação em 30/08, então aqui a
// equipe vai para o setor "Geral" da obra (criado na hora se não existir) em vez de exigir setor.
// Trabalhador pertence a UMA equipe (Trabalhador.EquipeId): quem já estava em outra é movido.
public record CriarEquipeComMembrosCommand(Guid ObraId, string Nome, Guid? EncarregadoId, List<Guid> TrabalhadorIds) : IRequest<Guid>;

public class CriarEquipeComMembrosCommandValidator : AbstractValidator<CriarEquipeComMembrosCommand>
{
    public CriarEquipeComMembrosCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TrabalhadorIds).NotEmpty().WithMessage("Marque ao menos um responsável para formar a equipe.");
    }
}

public class CriarEquipeComMembrosCommandHandler : IRequestHandler<CriarEquipeComMembrosCommand, Guid>
{
    public const string NomeSetorPadrao = "Geral";

    private readonly IAppDbContext _db;

    public CriarEquipeComMembrosCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarEquipeComMembrosCommand request, CancellationToken ct)
    {
        var obraExiste = await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct);
        if (!obraExiste) throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var nome = request.Nome.Trim();
        var nomeRepetido = await _db.Equipes.AnyAsync(e => e.Nome == nome && e.Setor!.ObraId == request.ObraId, ct);
        if (nomeRepetido)
            throw new InvalidOperationException($"Já existe uma equipe chamada \"{nome}\" nesta obra.");

        var ids = request.TrabalhadorIds.Distinct().ToList();
        var trabalhadores = await _db.Trabalhadores.Where(t => ids.Contains(t.Id)).ToListAsync(ct);
        if (trabalhadores.Count != ids.Count)
            throw new KeyNotFoundException("Um ou mais responsáveis marcados não foram encontrados.");

        if (request.EncarregadoId.HasValue && !ids.Contains(request.EncarregadoId.Value))
            throw new InvalidOperationException("O encarregado precisa ser um dos membros da equipe.");

        var setor = await _db.Setores.FirstOrDefaultAsync(s => s.ObraId == request.ObraId && s.Nome == NomeSetorPadrao, ct);
        if (setor is null)
        {
            setor = new Setor { ObraId = request.ObraId, Nome = NomeSetorPadrao };
            _db.Setores.Add(setor);
        }

        var equipe = new Equipe { Setor = setor, Nome = nome, EncarregadoId = request.EncarregadoId };
        _db.Equipes.Add(equipe);
        foreach (var trabalhador in trabalhadores)
            trabalhador.Equipe = equipe;

        await _db.SaveChangesAsync(ct);
        return equipe.Id;
    }
}
