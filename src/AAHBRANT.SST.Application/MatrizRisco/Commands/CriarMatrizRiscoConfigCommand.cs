using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.MatrizRisco.Commands;

public record CriarMatrizRiscoConfigCelula(int Probabilidade, int Severidade, Domain.Enums.NivelRisco NivelRisco);

public record CriarMatrizRiscoConfigCommand(
    string Nome,
    int NumNiveisProbabilidade,
    int NumNiveisSeveridade,
    List<CriarMatrizRiscoConfigCelula> Celulas) : IRequest<Guid>;

public class CriarMatrizRiscoConfigCommandValidator : AbstractValidator<CriarMatrizRiscoConfigCommand>
{
    public CriarMatrizRiscoConfigCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NumNiveisProbabilidade).GreaterThan(0);
        RuleFor(x => x.NumNiveisSeveridade).GreaterThan(0);
        RuleFor(x => x.Celulas).NotEmpty();
        RuleForEach(x => x.Celulas).ChildRules(c =>
        {
            c.RuleFor(x => x.Probabilidade).GreaterThan(0);
            c.RuleFor(x => x.Severidade).GreaterThan(0);
        });
    }
}

public class CriarMatrizRiscoConfigCommandHandler : IRequestHandler<CriarMatrizRiscoConfigCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarMatrizRiscoConfigCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarMatrizRiscoConfigCommand request, CancellationToken ct)
    {
        var config = new MatrizRiscoConfig
        {
            Nome = request.Nome,
            NumNiveisProbabilidade = request.NumNiveisProbabilidade,
            NumNiveisSeveridade = request.NumNiveisSeveridade
        };

        foreach (var celula in request.Celulas)
        {
            config.Celulas.Add(new MatrizRiscoCelula
            {
                Probabilidade = celula.Probabilidade,
                Severidade = celula.Severidade,
                NivelRisco = celula.NivelRisco
            });
        }

        _db.MatrizRiscoConfigs.Add(config);
        await _db.SaveChangesAsync(ct);
        return config.Id;
    }
}
