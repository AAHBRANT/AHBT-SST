using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Queries;
using AAHBRANT.SST.Application.PlanosIntegracao.Queries;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;

// Conclui o atendimento na etapa da OS (decisão de 11/11/2026 + Q2): libera quando todas as OS emitíveis estão assinadas
// (funcionário e técnico). Quem ficou com pendência externa (ex.: Sem ASO apto) fica "OS pendente" e recebe o registro
// "Integração realizada" ao assinar a OS depois. Cria o RegistroIntegracao de cada pessoa com OS válida e recalcula os
// planos de entrada. Idempotente: atendimento já concluído devolve o estado atual.
public record ConcluirIntegracaoCommand(
    Guid IntegracaoId,
    string? AzureAdObjectId = null,
    bool AutenticacaoHabilitada = false) : IRequest<IntegracaoAtendimentoDto>;

public class ConcluirIntegracaoCommandValidator : AbstractValidator<ConcluirIntegracaoCommand>
{
    public ConcluirIntegracaoCommandValidator() => RuleFor(x => x.IntegracaoId).NotEmpty();
}

public class ConcluirIntegracaoCommandHandler : IRequestHandler<ConcluirIntegracaoCommand, IntegracaoAtendimentoDto>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<ConcluirIntegracaoCommandHandler>? _logger;

    public ConcluirIntegracaoCommandHandler(IAppDbContext db, IMediator mediator, ILogger<ConcluirIntegracaoCommandHandler>? logger = null)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<IntegracaoAtendimentoDto> Handle(ConcluirIntegracaoCommand request, CancellationToken ct)
    {
        var (at, _) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        if (at.Status == StatusIntegracaoAtendimento.Concluido)
            return await MontadorIntegracaoAtendimento.MontarAsync(_db, at.Id, ct) ?? throw new KeyNotFoundException("Integração não encontrada.");
        RegrasParticipantesIntegracao.ExigirEmAndamento(at);

        var etapas = await _db.IntegracaoEtapas.Where(e => e.IntegracaoAtendimentoId == at.Id).OrderBy(e => e.Ordem).ToListAsync(ct);
        var etapaOs = etapas.FirstOrDefault(e => e.Tipo == TipoEtapaIntegracao.OrdemServico && e.Status == StatusEtapaIntegracao.EmAndamento)
            ?? throw new InvalidOperationException("A integração só conclui na última etapa (Ordem de Serviço). Conclua as etapas de curso antes.");

        var os = await _mediator.Send(new ListarOsIntegracaoQuery(at.Id, request.AzureAdObjectId, request.AutenticacaoHabilitada), ct);
        if (!AvaliadorConclusaoIntegracao.PodeConcluir(os.Pessoas))
        {
            var faltam = os.Pessoas.Where(p => p.Situacao is not (SituacaoOsIntegracao.Assinada or SituacaoOsIntegracao.Pendente))
                .Select(p => p.Situacao == SituacaoOsIntegracao.Bloqueada
                    ? $"{p.Nome}: pendência que segura a conclusão — {string.Join("; ", p.Pendencias)}."
                    : $"{p.Nome}: OS ainda não assinada.").ToList();
            if (faltam.Count == 0) faltam.Add("Nenhuma Ordem de Serviço assinada ainda: é preciso ao menos uma para concluir.");
            throw new IntegracaoBloqueadaException(
                $"{faltam.Count} Ordem(ns) de Serviço ainda precisam ser assinadas antes de concluir a integração.", faltam);
        }

        etapaOs.Status = StatusEtapaIntegracao.Concluida;
        etapaOs.ConcluidaEmUtc = DateTime.UtcNow;
        at.Status = StatusIntegracaoAtendimento.Concluido;
        at.ConcluidaEmUtc = DateTime.UtcNow;
        at.EtapaAtualOrdem = null;
        at.Versao++;
        await RegistroIntegracaoSincronizador.SincronizarAtendimentoAsync(_db, at, ct);
        await _db.SaveChangesAsync(ct);

        // Recalcula os planos de entrada dessas pessoas (o passo "Integração" passa a Feito; plano sem mais nada pendente fecha).
        // Melhor esforço: o atendimento já está concluído.
        try
        {
            var planos = await _db.ParticipantesIntegracao
                .Where(p => p.IntegracaoAtendimentoId == at.Id && p.PlanoIntegracaoId != null)
                .Select(p => p.PlanoIntegracaoId!.Value).ToListAsync(ct);
            if (planos.Count > 0)
                await _mediator.Send(new ListarPlanosIntegracaoPendentesQuery(
                    null, request.AzureAdObjectId, request.AutenticacaoHabilitada, at.ObraId, false, planos), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Não foi possível recalcular os planos de integração ao concluir o atendimento {Atendimento}.", at.Id);
        }

        return await MontadorIntegracaoAtendimento.MontarAsync(_db, at.Id, ct) ?? throw new KeyNotFoundException("Integração não encontrada.");
    }
}
