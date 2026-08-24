using AAHBRANT.SST.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Elegibilidade.Queries;

public record AvaliarElegibilidadeQuery(
    Guid TrabalhadorId,
    Guid ObraId,
    Guid? AtividadeId,
    Guid? TipoAutorizacaoId,
    Guid? PermissaoTrabalhoId,
    string ContextoModulo) : IRequest<EligibilityResult>;

public class AvaliarElegibilidadeQueryValidator : AbstractValidator<AvaliarElegibilidadeQuery>
{
    public AvaliarElegibilidadeQueryValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.ContextoModulo).NotEmpty();
    }
}

public class AvaliarElegibilidadeQueryHandler : IRequestHandler<AvaliarElegibilidadeQuery, EligibilityResult>
{
    private readonly IEligibilityService _eligibilityService;

    public AvaliarElegibilidadeQueryHandler(IEligibilityService eligibilityService)
        => _eligibilityService = eligibilityService;

    public Task<EligibilityResult> Handle(AvaliarElegibilidadeQuery request, CancellationToken ct)
    {
        var eligibilityRequest = new EligibilityRequest
        {
            TrabalhadorId = request.TrabalhadorId,
            ObraId = request.ObraId,
            AtividadeId = request.AtividadeId,
            TipoAutorizacaoId = request.TipoAutorizacaoId,
            PermissaoTrabalhoId = request.PermissaoTrabalhoId,
            ContextoModulo = request.ContextoModulo
        };

        return _eligibilityService.AvaliarAsync(eligibilityRequest, ct);
    }
}
