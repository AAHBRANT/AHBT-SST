using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SuporteIa;
using AAHBRANT.SST.Application.SuporteIa.Queries;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SuporteIa.Commands;

// Etapa 4 (Validação do solicitante) — fecha o ciclo. Vale tanto para o caso simples (a IA só
// respondeu, sem demanda técnica: Respondida) quanto para o caso que passou pela etapa 3
// (AguardandoValidacao). Confirmado=true encerra (Resolvida); false reabre para o responsável
// reanalisar (Reaberta), sem precisar abrir um chamado novo.
public record ValidarSolicitacaoSuporteIaCommand(
    Guid Id,
    bool Confirmado,
    string? Comentario,
    Guid? SolicitanteUsuarioId,
    string? SolicitanteEmail) : IRequest<SuporteIaSolicitacaoDto>;

public class ValidarSolicitacaoSuporteIaCommandValidator : AbstractValidator<ValidarSolicitacaoSuporteIaCommand>
{
    public ValidarSolicitacaoSuporteIaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Comentario).MaximumLength(4000);
        RuleFor(x => x.Comentario)
            .NotEmpty()
            .WithMessage("Explique o que ainda não foi resolvido.")
            .When(x => !x.Confirmado);
    }
}

public class ValidarSolicitacaoSuporteIaCommandHandler
    : IRequestHandler<ValidarSolicitacaoSuporteIaCommand, SuporteIaSolicitacaoDto>
{
    private readonly IAppDbContext _db;
    private readonly IFilaCalendarioTeams _filaCalendario;

    public ValidarSolicitacaoSuporteIaCommandHandler(IAppDbContext db, IFilaCalendarioTeams filaCalendario)
    {
        _db = db;
        _filaCalendario = filaCalendario;
    }

    public async Task<SuporteIaSolicitacaoDto> Handle(ValidarSolicitacaoSuporteIaCommand request, CancellationToken ct)
    {
        var solicitacao = await _db.SuporteIaSolicitacoes.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Chamado {request.Id} não encontrado.");

        if (solicitacao.Status is not (StatusSolicitacaoSuporteIa.Respondida or StatusSolicitacaoSuporteIa.AguardandoValidacao))
            throw new InvalidOperationException("Este chamado não está aguardando validação.");

        var ehOSolicitante = ListarSolicitacoesSuporteIaQueryHandler.EhOSolicitante(
            new SuporteIaSolicitacaoDto { SolicitanteUsuarioId = solicitacao.SolicitanteUsuarioId, SolicitanteEmail = solicitacao.SolicitanteEmail },
            request.SolicitanteUsuarioId,
            request.SolicitanteEmail);

        var solicitacaoSemIdentidade = solicitacao.SolicitanteUsuarioId is null && string.IsNullOrWhiteSpace(solicitacao.SolicitanteEmail);
        if (!ehOSolicitante && !solicitacaoSemIdentidade)
            throw new InvalidOperationException("Só quem abriu o chamado pode validar o fechamento.");

        solicitacao.Status = request.Confirmado ? StatusSolicitacaoSuporteIa.Resolvida : StatusSolicitacaoSuporteIa.Reaberta;
        solicitacao.ValidacaoConfirmada = request.Confirmado;
        solicitacao.ComentarioValidacao = string.IsNullOrWhiteSpace(request.Comentario) ? null : request.Comentario.Trim();
        solicitacao.ValidadoEmUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        if (solicitacao.Status == StatusSolicitacaoSuporteIa.Resolvida)
            await SuporteIaCalendario.EncerrarAsync(_db, _filaCalendario, solicitacao, ct);

        return SuporteIaMapeador.Mapear(solicitacao, solicitacao.SolicitanteUsuarioId, solicitacao.SolicitanteEmail);
    }
}
