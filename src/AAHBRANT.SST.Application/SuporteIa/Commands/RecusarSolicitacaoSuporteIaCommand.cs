using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SuporteIa;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SuporteIa.Commands;

// Desvio da etapa 3: o responsável recusa a demanda técnica (não procede, duplicada etc.) em vez de
// executá-la. Motivo obrigatório — fica registrado para quem abriu o chamado entender o encerramento.
public record RecusarSolicitacaoSuporteIaCommand(Guid Id, string NotaFechamento) : IRequest<SuporteIaSolicitacaoDto>;

public class RecusarSolicitacaoSuporteIaCommandValidator : AbstractValidator<RecusarSolicitacaoSuporteIaCommand>
{
    public RecusarSolicitacaoSuporteIaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NotaFechamento).NotEmpty().MaximumLength(4000);
    }
}

public class RecusarSolicitacaoSuporteIaCommandHandler
    : IRequestHandler<RecusarSolicitacaoSuporteIaCommand, SuporteIaSolicitacaoDto>
{
    private readonly IAppDbContext _db;
    private readonly IFilaCalendarioTeams _filaCalendario;

    public RecusarSolicitacaoSuporteIaCommandHandler(IAppDbContext db, IFilaCalendarioTeams filaCalendario)
    {
        _db = db;
        _filaCalendario = filaCalendario;
    }

    public async Task<SuporteIaSolicitacaoDto> Handle(RecusarSolicitacaoSuporteIaCommand request, CancellationToken ct)
    {
        var solicitacao = await _db.SuporteIaSolicitacoes.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Chamado {request.Id} não encontrado.");

        if (solicitacao.Status is not (StatusSolicitacaoSuporteIa.Encaminhada
            or StatusSolicitacaoSuporteIa.EmAnaliseTecnica
            or StatusSolicitacaoSuporteIa.Reaberta))
        {
            throw new InvalidOperationException("Este chamado não pode mais ser recusado no estado atual.");
        }

        solicitacao.Status = StatusSolicitacaoSuporteIa.Cancelada;
        solicitacao.NotaFechamento = request.NotaFechamento.Trim();
        solicitacao.ConcluidoEmUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await SuporteIaCalendario.EncerrarAsync(_db, _filaCalendario, solicitacao, ct);

        return SuporteIaMapeador.Mapear(solicitacao, solicitacao.SolicitanteUsuarioId, solicitacao.SolicitanteEmail);
    }
}
