using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Commands;

// "Aguardando laudo": pausa a investigação (ex.: óbito, à espera do laudo oficial) sem concluí-la.
// Em investigação → Aguardando laudo → (retomar) → Em investigação. Não publica ao G-RH: o evento leva o
// nome do status e o G-RH não conhece "AguardandoLaudo"; para ele a ocorrência segue "EmInvestigacao".
public record AguardarLaudoCommand(Guid Id, string? Observacao) : IRequest;

public class AguardarLaudoCommandValidator : AbstractValidator<AguardarLaudoCommand>
{
    public AguardarLaudoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Observacao).MaximumLength(500);
    }
}

public class AguardarLaudoCommandHandler : IRequestHandler<AguardarLaudoCommand>
{
    private readonly IAppDbContext _db;

    public AguardarLaudoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AguardarLaudoCommand request, CancellationToken ct)
    {
        var acidente = await _db.Acidentes.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Acidente {request.Id} não encontrado.");

        if (acidente.Status != StatusAcidente.EmInvestigacao)
            throw new InvalidOperationException("Só uma ocorrência em investigação pode ficar aguardando laudo.");

        acidente.Status = StatusAcidente.AguardandoLaudo;
        acidente.AguardandoLaudoDesde = DateTime.UtcNow;
        acidente.LaudoObservacao = string.IsNullOrWhiteSpace(request.Observacao) ? null : request.Observacao.Trim();
        await _db.SaveChangesAsync(ct);
    }
}

public record RetomarInvestigacaoCommand(Guid Id) : IRequest;

public class RetomarInvestigacaoCommandValidator : AbstractValidator<RetomarInvestigacaoCommand>
{
    public RetomarInvestigacaoCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class RetomarInvestigacaoCommandHandler : IRequestHandler<RetomarInvestigacaoCommand>
{
    private readonly IAppDbContext _db;

    public RetomarInvestigacaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RetomarInvestigacaoCommand request, CancellationToken ct)
    {
        var acidente = await _db.Acidentes.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Acidente {request.Id} não encontrado.");

        if (acidente.Status != StatusAcidente.AguardandoLaudo)
            throw new InvalidOperationException("A ocorrência não está aguardando laudo.");

        acidente.Status = StatusAcidente.EmInvestigacao;
        // A observação e a data da pausa ficam no registro como histórico do que aconteceu.
        await _db.SaveChangesAsync(ct);
    }
}

public record ProvidenciaObitoEntrada(string Codigo, bool Concluida, string? Observacao);

// Checklist de providências do óbito (ver ProvidenciasObitoAcidente). Itens não enviados ficam como estão.
public record AtualizarProvidenciasObitoCommand(Guid Id, List<ProvidenciaObitoEntrada> Itens) : IRequest;

public class AtualizarProvidenciasObitoCommandValidator : AbstractValidator<AtualizarProvidenciasObitoCommand>
{
    public AtualizarProvidenciasObitoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Itens).NotNull();
        RuleForEach(x => x.Itens).ChildRules(i =>
        {
            i.RuleFor(x => x.Codigo).Must(c => ProvidenciasObitoAcidente.CodigosValidos.Contains(c))
                .WithMessage("Providência desconhecida: {PropertyValue}.");
            i.RuleFor(x => x.Observacao).MaximumLength(ProvidenciasObitoAcidente.MaximoObservacao);
        });
    }
}

public class AtualizarProvidenciasObitoCommandHandler : IRequestHandler<AtualizarProvidenciasObitoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarProvidenciasObitoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarProvidenciasObitoCommand request, CancellationToken ct)
    {
        var acidente = await _db.Acidentes.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Acidente {request.Id} não encontrado.");

        if (acidente.Status == StatusAcidente.Concluido)
            throw new InvalidOperationException("A investigação já foi concluída; as providências não podem mais ser alteradas.");
        var temObito = acidente.Gravidade == GravidadeAcidente.Obito
            || await _db.AcidentesPessoasExternas.AnyAsync(p => p.AcidenteId == acidente.Id && p.Resultado == ResultadoPessoaExterna.Obito, ct);
        if (!temObito)
            throw new InvalidOperationException("As providências de óbito só se aplicam a ocorrência com óbito (gravidade Óbito ou pessoa externa com resultado Óbito).");

        acidente.ProvidenciasObitoJson = ProvidenciasObitoAcidente.Gravar(
            acidente.ProvidenciasObitoJson,
            request.Itens.Select(i => (i.Codigo, i.Concluida, i.Observacao)),
            DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
    }
}
