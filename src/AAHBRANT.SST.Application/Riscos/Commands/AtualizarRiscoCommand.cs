using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Riscos.Commands;

public record AtualizarRiscoCommand(
    Guid Id,
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
    List<Guid> TrabalhadoresExpostosIds) : IRequest;

public class AtualizarRiscoCommandValidator : AbstractValidator<AtualizarRiscoCommand>
{
    public AtualizarRiscoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AtividadeId).NotEmpty();
        RuleFor(x => x.PerigoId).NotEmpty();
        RuleFor(x => x.Probabilidade).GreaterThan(0);
        RuleFor(x => x.Severidade).GreaterThan(0);
    }
}

public class AtualizarRiscoCommandHandler : IRequestHandler<AtualizarRiscoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarRiscoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarRiscoCommand request, CancellationToken ct)
    {
        var risco = await _db.Riscos
            .Include(r => r.TrabalhadoresExpostos)
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Risco {request.Id} não encontrado.");

        var nivelRisco = await NivelRiscoLookup.ResolverAsync(_db, request.AtividadeId, request.Probabilidade, request.Severidade, ct);

        risco.AtividadeId = request.AtividadeId;
        risco.PerigoId = request.PerigoId;
        risco.Ambiente = request.Ambiente;
        risco.Exposicao = request.Exposicao;
        risco.Consequencia = request.Consequencia;
        risco.Probabilidade = request.Probabilidade;
        risco.Severidade = request.Severidade;
        risco.NivelRisco = nivelRisco;
        risco.ControlesExistentes = request.ControlesExistentes;
        risco.ControlesAdicionais = request.ControlesAdicionais;
        risco.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
        risco.Prazo = request.Prazo;
        risco.Status = request.Status;

        var idsNovos = request.TrabalhadoresExpostosIds.Distinct().ToHashSet();
        foreach (var vinculoAntigo in risco.TrabalhadoresExpostos.Where(v => !idsNovos.Contains(v.TrabalhadorId)).ToList())
            _db.RiscoTrabalhadorExpostos.Remove(vinculoAntigo);

        var idsExistentes = risco.TrabalhadoresExpostos.Select(v => v.TrabalhadorId).ToHashSet();
        foreach (var trabalhadorId in idsNovos.Where(id => !idsExistentes.Contains(id)))
            risco.TrabalhadoresExpostos.Add(new RiscoTrabalhadorExposto { RiscoId = risco.Id, TrabalhadorId = trabalhadorId });

        await _db.SaveChangesAsync(ct);
    }
}
