using AAHBRANT.SST.Application.Aprs;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AprEtapaRiscos.Commands;

// Uma linha completa da tabela principal do formulário APR REV.02 — todos os campos literais da
// planilha (ver disclosure completo em AprEtapaRisco, Domain/Entidades/Apr/Apr.cs). NivelRiscoInicial
// e NivelRiscoResidual são calculados aqui (AprNivelRiscoCalculator), não informados pelo cliente.
public record CriarAprEtapaRiscoCommand(
    Guid AprEtapaId,
    string PerigoEventoPerigoso,
    string? FonteCircunstancia,
    string? PossiveisLesoes,
    string? TrabalhadoresExpostos,
    int ProbabilidadeInicial,
    int SeveridadeInicial,
    string? MedidasPrevencao,
    string? Responsavel,
    int ProbabilidadeResidual,
    int SeveridadeResidual) : IRequest<Guid>;

public class CriarAprEtapaRiscoCommandValidator : AbstractValidator<CriarAprEtapaRiscoCommand>
{
    public CriarAprEtapaRiscoCommandValidator()
    {
        RuleFor(x => x.AprEtapaId).NotEmpty();
        RuleFor(x => x.PerigoEventoPerigoso).NotEmpty().MaximumLength(300);
        RuleFor(x => x.FonteCircunstancia).MaximumLength(500);
        RuleFor(x => x.PossiveisLesoes).MaximumLength(500);
        RuleFor(x => x.TrabalhadoresExpostos).MaximumLength(300);
        RuleFor(x => x.MedidasPrevencao).MaximumLength(1000);
        RuleFor(x => x.Responsavel).MaximumLength(200);
        RuleFor(x => x.ProbabilidadeInicial).InclusiveBetween(1, 5);
        RuleFor(x => x.SeveridadeInicial).InclusiveBetween(1, 5);
        RuleFor(x => x.ProbabilidadeResidual).InclusiveBetween(1, 5);
        RuleFor(x => x.SeveridadeResidual).InclusiveBetween(1, 5);
    }
}

public class CriarAprEtapaRiscoCommandHandler : IRequestHandler<CriarAprEtapaRiscoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAprEtapaRiscoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAprEtapaRiscoCommand request, CancellationToken ct)
    {
        var etapaExiste = await _db.AprEtapas.AnyAsync(e => e.Id == request.AprEtapaId, ct);
        if (!etapaExiste)
            throw new KeyNotFoundException($"Etapa de APR {request.AprEtapaId} não encontrada.");

        var risco = new AprEtapaRisco
        {
            AprEtapaId = request.AprEtapaId,
            PerigoEventoPerigoso = request.PerigoEventoPerigoso,
            FonteCircunstancia = request.FonteCircunstancia,
            PossiveisLesoes = request.PossiveisLesoes,
            TrabalhadoresExpostos = request.TrabalhadoresExpostos,
            ProbabilidadeInicial = request.ProbabilidadeInicial,
            SeveridadeInicial = request.SeveridadeInicial,
            NivelRiscoInicial = AprNivelRiscoCalculator.Calcular(request.ProbabilidadeInicial, request.SeveridadeInicial),
            MedidasPrevencao = request.MedidasPrevencao,
            Responsavel = request.Responsavel,
            ProbabilidadeResidual = request.ProbabilidadeResidual,
            SeveridadeResidual = request.SeveridadeResidual,
            NivelRiscoResidual = AprNivelRiscoCalculator.Calcular(request.ProbabilidadeResidual, request.SeveridadeResidual),
        };

        _db.AprEtapaRiscos.Add(risco);
        await _db.SaveChangesAsync(ct);
        return risco.Id;
    }
}
