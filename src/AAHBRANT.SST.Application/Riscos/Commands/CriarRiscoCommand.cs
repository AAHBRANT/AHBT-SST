using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Riscos.Commands;

public record CriarRiscoCommand(
    Guid AtividadeId,
    Guid PerigoId,
    string? Ambiente,
    string? Exposicao,
    string? Consequencia,
    int Probabilidade,
    int Severidade,
    string? ControlesExistentes,
    string? ControlesAdicionais,
    Guid? ResponsavelUsuarioId,
    DateTime? Prazo,
    StatusControleRisco Status,
    List<Guid> TrabalhadoresExpostosIds) : IRequest<Guid>;

public class CriarRiscoCommandValidator : AbstractValidator<CriarRiscoCommand>
{
    public CriarRiscoCommandValidator()
    {
        RuleFor(x => x.AtividadeId).NotEmpty();
        RuleFor(x => x.PerigoId).NotEmpty();
        RuleFor(x => x.Probabilidade).GreaterThan(0);
        RuleFor(x => x.Severidade).GreaterThan(0);
    }
}

public class CriarRiscoCommandHandler : IRequestHandler<CriarRiscoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarRiscoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarRiscoCommand request, CancellationToken ct)
    {
        var nivelRisco = await NivelRiscoLookup.ResolverAsync(_db, request.AtividadeId, request.Probabilidade, request.Severidade, ct);

        var risco = new Risco
        {
            AtividadeId = request.AtividadeId,
            PerigoId = request.PerigoId,
            Ambiente = request.Ambiente,
            Exposicao = request.Exposicao,
            Consequencia = request.Consequencia,
            Probabilidade = request.Probabilidade,
            Severidade = request.Severidade,
            NivelRisco = nivelRisco,
            ControlesExistentes = request.ControlesExistentes,
            ControlesAdicionais = request.ControlesAdicionais,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            Prazo = request.Prazo,
            Status = request.Status
        };

        foreach (var trabalhadorId in request.TrabalhadoresExpostosIds.Distinct())
        {
            risco.TrabalhadoresExpostos.Add(new RiscoTrabalhadorExposto { TrabalhadorId = trabalhadorId });
        }

        _db.Riscos.Add(risco);
        await _db.SaveChangesAsync(ct);
        return risco.Id;
    }
}
