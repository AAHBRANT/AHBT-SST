using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MatrizRisco.Commands;

public record AtualizarMatrizRiscoConfigCommand(
    Guid Id,
    string Nome,
    int NumNiveisProbabilidade,
    int NumNiveisSeveridade,
    List<CriarMatrizRiscoConfigCelula> Celulas) : IRequest;

public class AtualizarMatrizRiscoConfigCommandValidator : AbstractValidator<AtualizarMatrizRiscoConfigCommand>
{
    public AtualizarMatrizRiscoConfigCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NumNiveisProbabilidade).GreaterThan(0);
        RuleFor(x => x.NumNiveisSeveridade).GreaterThan(0);
        RuleFor(x => x.Celulas).NotEmpty();
    }
}

public class AtualizarMatrizRiscoConfigCommandHandler : IRequestHandler<AtualizarMatrizRiscoConfigCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarMatrizRiscoConfigCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarMatrizRiscoConfigCommand request, CancellationToken ct)
    {
        var config = await _db.MatrizRiscoConfigs
            .Include(c => c.Celulas)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"MatrizRiscoConfig {request.Id} não encontrada.");

        config.Nome = request.Nome;
        config.NumNiveisProbabilidade = request.NumNiveisProbabilidade;
        config.NumNiveisSeveridade = request.NumNiveisSeveridade;

        foreach (var celulaAntiga in config.Celulas.ToList())
            _db.MatrizRiscoCelulas.Remove(celulaAntiga);

        foreach (var celula in request.Celulas)
        {
            config.Celulas.Add(new MatrizRiscoCelula
            {
                MatrizRiscoConfigId = config.Id,
                Probabilidade = celula.Probabilidade,
                Severidade = celula.Severidade,
                NivelRisco = celula.NivelRisco
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
