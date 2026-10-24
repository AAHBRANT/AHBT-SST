using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SuporteIa;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SuporteIa.Commands;

// Fim da etapa 3 — o responsável marca a correção/melhoria como pronta e o chamado passa para a
// etapa 4 (aguardando o solicitante confirmar que resolveu), em vez de ir direto para Resolvida.
public record ConcluirExecucaoSuporteIaCommand(Guid Id, string? NotaFechamento) : IRequest<SuporteIaSolicitacaoDto>;

public class ConcluirExecucaoSuporteIaCommandValidator : AbstractValidator<ConcluirExecucaoSuporteIaCommand>
{
    public ConcluirExecucaoSuporteIaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NotaFechamento).MaximumLength(4000);
    }
}

public class ConcluirExecucaoSuporteIaCommandHandler
    : IRequestHandler<ConcluirExecucaoSuporteIaCommand, SuporteIaSolicitacaoDto>
{
    private readonly IAppDbContext _db;

    public ConcluirExecucaoSuporteIaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<SuporteIaSolicitacaoDto> Handle(ConcluirExecucaoSuporteIaCommand request, CancellationToken ct)
    {
        var solicitacao = await _db.SuporteIaSolicitacoes.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Chamado {request.Id} não encontrado.");

        if (solicitacao.Status != StatusSolicitacaoSuporteIa.EmAnaliseTecnica)
            throw new InvalidOperationException("Só é possível concluir um chamado que esteja em análise técnica.");

        solicitacao.Status = StatusSolicitacaoSuporteIa.AguardandoValidacao;
        solicitacao.NotaFechamento = string.IsNullOrWhiteSpace(request.NotaFechamento) ? null : request.NotaFechamento.Trim();
        solicitacao.ConcluidoEmUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return SuporteIaMapeador.Mapear(solicitacao, solicitacao.SolicitanteUsuarioId, solicitacao.SolicitanteEmail);
    }
}
