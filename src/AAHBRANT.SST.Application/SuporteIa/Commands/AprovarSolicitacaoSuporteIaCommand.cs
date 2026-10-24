using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SuporteIa;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SuporteIa.Commands;

// Etapa 3 da esteira (Aprovação e execução) — o responsável assume o chamado encaminhado pela IA e
// começa a tratar. Só sai de Encaminhada/Reaberta; EmAnaliseTecnica é o "em execução".
public record AprovarSolicitacaoSuporteIaCommand(
    Guid Id,
    Guid? ResponsavelUsuarioId,
    string? ResponsavelNome) : IRequest<SuporteIaSolicitacaoDto>;

public class AprovarSolicitacaoSuporteIaCommandValidator : AbstractValidator<AprovarSolicitacaoSuporteIaCommand>
{
    public AprovarSolicitacaoSuporteIaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class AprovarSolicitacaoSuporteIaCommandHandler
    : IRequestHandler<AprovarSolicitacaoSuporteIaCommand, SuporteIaSolicitacaoDto>
{
    private readonly IAppDbContext _db;

    public AprovarSolicitacaoSuporteIaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<SuporteIaSolicitacaoDto> Handle(AprovarSolicitacaoSuporteIaCommand request, CancellationToken ct)
    {
        var solicitacao = await _db.SuporteIaSolicitacoes.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Chamado {request.Id} não encontrado.");

        if (solicitacao.Status is not (StatusSolicitacaoSuporteIa.Encaminhada or StatusSolicitacaoSuporteIa.Reaberta))
            throw new InvalidOperationException("Só é possível aprovar um chamado encaminhado ou reaberto.");

        solicitacao.Status = StatusSolicitacaoSuporteIa.EmAnaliseTecnica;
        solicitacao.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
        solicitacao.ResponsavelNome = string.IsNullOrWhiteSpace(request.ResponsavelNome) ? null : request.ResponsavelNome.Trim();
        solicitacao.AprovadoEmUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return SuporteIaMapeador.Mapear(solicitacao, solicitacao.SolicitanteUsuarioId, solicitacao.SolicitanteEmail);
    }
}
