using AAHBRANT.SST.Application.Aprs;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AprEtapaRiscos.Commands;

public record AtualizarAprEtapaRiscoCommand(
    Guid Id,
    string PerigoEventoPerigoso,
    string? FonteCircunstancia,
    string? PossiveisLesoes,
    string? TrabalhadoresExpostos,
    int ProbabilidadeInicial,
    int SeveridadeInicial,
    string? MedidasPrevencao,
    string? Responsavel,
    int ProbabilidadeResidual,
    int SeveridadeResidual) : IRequest;

public class AtualizarAprEtapaRiscoCommandValidator : AbstractValidator<AtualizarAprEtapaRiscoCommand>
{
    public AtualizarAprEtapaRiscoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
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

public class AtualizarAprEtapaRiscoCommandHandler : IRequestHandler<AtualizarAprEtapaRiscoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarAprEtapaRiscoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarAprEtapaRiscoCommand request, CancellationToken ct)
    {
        var risco = await _db.AprEtapaRiscos.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Risco de etapa de APR {request.Id} não encontrado.");

        risco.PerigoEventoPerigoso = request.PerigoEventoPerigoso;
        risco.FonteCircunstancia = request.FonteCircunstancia;
        risco.PossiveisLesoes = request.PossiveisLesoes;
        risco.TrabalhadoresExpostos = request.TrabalhadoresExpostos;
        risco.ProbabilidadeInicial = request.ProbabilidadeInicial;
        risco.SeveridadeInicial = request.SeveridadeInicial;
        risco.NivelRiscoInicial = AprNivelRiscoCalculator.Calcular(request.ProbabilidadeInicial, request.SeveridadeInicial);
        risco.MedidasPrevencao = request.MedidasPrevencao;
        risco.Responsavel = request.Responsavel;
        risco.ProbabilidadeResidual = request.ProbabilidadeResidual;
        risco.SeveridadeResidual = request.SeveridadeResidual;
        risco.NivelRiscoResidual = AprNivelRiscoCalculator.Calcular(request.ProbabilidadeResidual, request.SeveridadeResidual);

        await _db.SaveChangesAsync(ct);
    }
}
