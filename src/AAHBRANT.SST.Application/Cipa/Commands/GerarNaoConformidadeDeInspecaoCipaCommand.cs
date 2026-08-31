using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.NaoConformidades.Commands;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

// Ponte manual entre uma InspecaoCipa e o módulo de Não Conformidade já existente — o pedido do
// usuário descreve uma integração automática "Inspeção CIPA → inventário de riscos do PGR/GRO", mas
// isso exigiria alterações estruturais no módulo de Riscos fora do escopo desta fatia (ver disclosure
// em Domain/Entidades/Cipa/Cipa.cs). Reaproveita CriarNaoConformidadeCommand (Origem=Inspecao) em vez
// de duplicar a lógica de criação.
public record GerarNaoConformidadeDeInspecaoCipaCommand(
    Guid InspecaoCipaId,
    Guid? ResponsavelUsuarioId,
    DateTime? Prazo) : IRequest<Guid>;

public class GerarNaoConformidadeDeInspecaoCipaCommandValidator : AbstractValidator<GerarNaoConformidadeDeInspecaoCipaCommand>
{
    public GerarNaoConformidadeDeInspecaoCipaCommandValidator() => RuleFor(x => x.InspecaoCipaId).NotEmpty();
}

public class GerarNaoConformidadeDeInspecaoCipaCommandHandler : IRequestHandler<GerarNaoConformidadeDeInspecaoCipaCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;

    public GerarNaoConformidadeDeInspecaoCipaCommandHandler(IAppDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<Guid> Handle(GerarNaoConformidadeDeInspecaoCipaCommand request, CancellationToken ct)
    {
        var inspecao = await _db.InspecoesCipa.FirstOrDefaultAsync(i => i.Id == request.InspecaoCipaId, ct)
            ?? throw new KeyNotFoundException($"Inspeção da CIPA {request.InspecaoCipaId} não encontrada.");

        if (inspecao.NaoConformidadeId.HasValue)
            throw new InvalidOperationException("Esta inspeção já gerou uma Não Conformidade.");

        var ncId = await _mediator.Send(new CriarNaoConformidadeCommand(
            OrigemNaoConformidade.Inspecao,
            "Inspeção CIPA",
            inspecao.RiscoIdentificado,
            inspecao.Local,
            null,
            null,
            request.ResponsavelUsuarioId,
            request.Prazo), ct);

        inspecao.NaoConformidadeId = ncId;
        await _db.SaveChangesAsync(ct);
        return ncId;
    }
}
