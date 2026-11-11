using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;

// "Avançar" a etapa de curso em andamento (decisão de 11/11/2026). Exige instrutor, carga lançada >= mínimo (0 não
// trava), 3 fotos e a assinatura biométrica de todos os participantes da etapa. Então grava a carga e a data (último dia)
// na turma, encerra a turma (um certificado por pessoa, cada um com o próprio documento de assinatura), marca a etapa
// Concluída e abre a próxima que tem gente. Sem volta: etapa concluída é imutável.
//
// Idempotente e retomável: EtapaOrdem informada que já está Concluída devolve o estado atual (duplo clique); se uma
// chamada anterior encerrou a turma mas não chegou a gravar a etapa, a nova só completa a transição.
// Faltas => IntegracaoBloqueadaException (a API responde 409 com a lista).
public record AvancarEtapaIntegracaoCommand(
    Guid IntegracaoId,
    int? EtapaOrdem = null,
    string? AzureAdObjectId = null,
    bool AutenticacaoHabilitada = false) : IRequest<IntegracaoAtendimentoDto>;

public class AvancarEtapaIntegracaoCommandValidator : AbstractValidator<AvancarEtapaIntegracaoCommand>
{
    public AvancarEtapaIntegracaoCommandValidator() => RuleFor(x => x.IntegracaoId).NotEmpty();
}

public class AvancarEtapaIntegracaoCommandHandler : IRequestHandler<AvancarEtapaIntegracaoCommand, IntegracaoAtendimentoDto>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public AvancarEtapaIntegracaoCommandHandler(IAppDbContext db, IMediator mediator, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _mediator = mediator;
        _geradorNumero = geradorNumero;
    }

    public async Task<IntegracaoAtendimentoDto> Handle(AvancarEtapaIntegracaoCommand request, CancellationToken ct)
    {
        var (at, _) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        var etapas = await _db.IntegracaoEtapas.Where(e => e.IntegracaoAtendimentoId == at.Id).OrderBy(e => e.Ordem).ToListAsync(ct);

        // Duplo clique / reenvio: a etapa pedida já foi concluída, nada a fazer.
        if (request.EtapaOrdem is { } pedida && etapas.FirstOrDefault(e => e.Ordem == pedida) is { Status: StatusEtapaIntegracao.Concluida })
            return await MontadorIntegracaoAtendimento.MontarAsync(_db, at.Id, ct) ?? throw new KeyNotFoundException("Integração não encontrada.");

        if (at.Status != StatusIntegracaoAtendimento.EmAndamento)
            throw new InvalidOperationException("Esta integração já foi encerrada.");
        var atual = etapas.FirstOrDefault(e => e.Ordem == at.EtapaAtualOrdem && e.Status == StatusEtapaIntegracao.EmAndamento)
            ?? throw new InvalidOperationException("A integração não tem etapa em andamento.");
        if (request.EtapaOrdem is { } ordem && ordem != atual.Ordem)
            throw new InvalidOperationException("A etapa informada não é a etapa atual da integração.");
        if (atual.Tipo == TipoEtapaIntegracao.OrdemServico)
            throw new InvalidOperationException("A última etapa (Ordem de Serviço) é concluída em \"Concluir integração\".");

        var sessao = await _db.SessoesTreinamento.FirstOrDefaultAsync(s => s.Id == atual.SessaoTreinamentoId, ct)
            ?? throw new InvalidOperationException("A turma desta etapa não foi encontrada.");
        var participantesTurma = await _db.ParticipantesSessaoTreinamento
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.Ativo).ToListAsync(ct);
        var periodos = await _db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == atual.Id).ToListAsync(ct);
        var minutos = periodos.Sum(p => p.Minutos);
        var fotos = await _db.FotosEvidenciaSessaoTreinamento.CountAsync(f => f.SessaoTreinamentoId == sessao.Id && f.Ativo, ct);
        var nomes = await _db.ParticipantesIntegracao.Where(p => p.IntegracaoAtendimentoId == at.Id)
            .ToDictionaryAsync(p => p.TrabalhadorId, p => p.Nome, ct);

        var marcados = await _db.NaoAssinouEtapasIntegracao.CountAsync(n => n.IntegracaoEtapaId == atual.Id, ct);
        var todosNaoAssinaram = participantesTurma.Count == 0 && marcados > 0;
        var retomando = sessao.Status == StatusSessaoTreinamento.Concluida;
        if (!retomando && todosNaoAssinaram)
        {
            // Q9: todos da etapa estão como "Não assinou": não há certificado, ata nem foto a exigir; a turma só fecha.
            sessao.Status = StatusSessaoTreinamento.Concluida;
            sessao.DataEncerramento = DateTime.UtcNow;
        }
        else if (!retomando)
        {
            var faltas = AvaliadorEtapaIntegracao.Faltas(atual, sessao, participantesTurma, nomes, minutos, fotos);
            if (faltas.Count > 0)
                throw new IntegracaoBloqueadaException(
                    $"A etapa {CalculadoraEtapasIntegracao.Titulo(atual.Tipo)} ainda não pode avançar: {faltas.Count} pendência(s).", faltas);

            // Carga do certificado: a lançada de verdade, em horas inteiras (arredondada para baixo, mínimo 1 h, porque o campo do
            // certificado é inteiro); nunca a carga padrão do curso. A data de cada certificado é a da assinatura de cada pessoa (Q6).
            sessao.CargaHorariaRealizada = Math.Max(1, minutos / 60);
            if (periodos.Count > 0) sessao.DataRealizacao = periodos.Max(p => p.Data);

            // Encerra a turma (certificados + assinaturas). Se falhar antes de gravar, nada da etapa muda.
            await _mediator.Send(new EncerrarSessaoTreinamentoCommand(sessao.Id, request.AzureAdObjectId, ViaIntegracao: true), ct);
        }

        atual.Status = StatusEtapaIntegracao.Concluida;
        atual.ConcluidaEmUtc = DateTime.UtcNow;
        await AberturaEtapaIntegracao.AbrirProximaAsync(_db, _geradorNumero, at, etapas, atual.Ordem + 1, ct);
        at.Versao++;
        await _db.SaveChangesAsync(ct);

        return await MontadorIntegracaoAtendimento.MontarAsync(_db, at.Id, ct) ?? throw new KeyNotFoundException("Integração não encontrada.");
    }
}
