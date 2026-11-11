using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;

// Q5 (padrão adotado em 11/11/2026): pessoas entram e saem do atendimento (com ou sem plano de entrada aberto)
// enquanto a primeira etapa de curso em andamento não tiver nenhuma assinatura.
internal static class RegrasParticipantesIntegracao
{
    public static void ExigirEmAndamento(IntegracaoAtendimento at)
    {
        if (at.Status != StatusIntegracaoAtendimento.EmAndamento)
            throw new InvalidOperationException("Esta integração já foi encerrada.");
    }

    public static async Task ExigirPodeEditarAsync(IAppDbContext db, IntegracaoAtendimento at, IReadOnlyList<IntegracaoEtapa> etapas, CancellationToken ct)
    {
        var primeira = etapas.Where(e => e.Tipo != TipoEtapaIntegracao.OrdemServico && e.Status != StatusEtapaIntegracao.Pulada)
            .OrderBy(e => e.Ordem).FirstOrDefault();
        if (primeira is null) return;
        var comAssinatura = primeira.Status != StatusEtapaIntegracao.EmAndamento
            || await db.ParticipantesSessaoTreinamento.AnyAsync(p => p.SessaoTreinamentoId == primeira.SessaoTreinamentoId
                && p.Ativo && p.PresencaConfirmadaEm != null, ct);
        if (comAssinatura)
            throw new InvalidOperationException(
                "A primeira etapa já tem assinatura registrada. Depois disso a lista de pessoas não muda mais; inicie outra integração para quem faltou.");
    }
}

public record IncluirParticipantesIntegracaoCommand(
    Guid IntegracaoId,
    List<Guid> TrabalhadoresIds,
    string? AzureAdObjectId = null,
    bool AutenticacaoHabilitada = false) : IRequest;

public class IncluirParticipantesIntegracaoCommandValidator : AbstractValidator<IncluirParticipantesIntegracaoCommand>
{
    public IncluirParticipantesIntegracaoCommandValidator()
    {
        RuleFor(x => x.IntegracaoId).NotEmpty();
        RuleFor(x => x.TrabalhadoresIds).NotEmpty().WithMessage("Selecione ao menos uma pessoa.");
    }
}

public class IncluirParticipantesIntegracaoCommandHandler : IRequestHandler<IncluirParticipantesIntegracaoCommand>
{
    private readonly IAppDbContext _db;

    public IncluirParticipantesIntegracaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(IncluirParticipantesIntegracaoCommand request, CancellationToken ct)
    {
        var (at, _) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        RegrasParticipantesIntegracao.ExigirEmAndamento(at);
        var etapas = await _db.IntegracaoEtapas.Where(e => e.IntegracaoAtendimentoId == at.Id).OrderBy(e => e.Ordem).ToListAsync(ct);
        await RegrasParticipantesIntegracao.ExigirPodeEditarAsync(_db, at, etapas, ct);

        var ids = request.TrabalhadoresIds.Where(i => i != Guid.Empty).Distinct().ToList();
        var jaParticipam = await _db.ParticipantesIntegracao.Where(p => p.IntegracaoAtendimentoId == at.Id && ids.Contains(p.TrabalhadorId))
            .Select(p => p.TrabalhadorId).ToListAsync(ct);
        ids = ids.Except(jaParticipam).ToList();
        if (ids.Count == 0) return;

        var trabalhadores = await _db.Trabalhadores
            .Where(t => ids.Contains(t.Id) && t.ObraId == at.ObraId && t.Situacao == SituacaoTrabalhador.Ativo
                && t.Vinculo != TipoVinculo.PrestadorServico)
            .ToListAsync(ct);
        if (trabalhadores.Count != ids.Count)
            throw new InvalidOperationException("Há pessoas que não estão ativas nesta obra. Atualize a lista e tente de novo.");

        var obraNome = await _db.Obras.Where(o => o.Id == at.ObraId).Select(o => o.Nome).FirstOrDefaultAsync(ct);
        var cursos = await ExigenciasEtapasIntegracao.CarregarCursosAsync(_db, obraNome, ct);
        var pessoas = await ExigenciasEtapasIntegracao.CarregarPessoasAsync(_db, trabalhadores, at.ObraId, ct);
        var calculo = CalculadoraEtapasIntegracao.Calcular(pessoas, cursos);

        var atual = at.EtapaAtualOrdem ?? 0;
        foreach (var c in calculo.Where(c => c.Tipo != TipoEtapaIntegracao.OrdemServico && c.Participantes.Count > 0))
        {
            var etapa = etapas.First(e => e.Tipo == c.Tipo);
            var titulo = CalculadoraEtapasIntegracao.Titulo(c.Tipo);
            switch (etapa.Status)
            {
                case StatusEtapaIntegracao.Concluida:
                    throw new InvalidOperationException($"Alguém da lista precisa da etapa {titulo}, que já foi concluída. Inicie outra integração para essa pessoa.");
                case StatusEtapaIntegracao.Pulada when etapa.Ordem < atual:
                    throw new InvalidOperationException($"Alguém da lista precisa da etapa {titulo}, que foi pulada e já ficou para trás. Inicie outra integração para essa pessoa.");
                case StatusEtapaIntegracao.Pulada:
                    if (c.CursoId is null)
                        throw new InvalidOperationException($"O curso {CalculadoraEtapasIntegracao.NormaDoTipo(c.Tipo)} não está cadastrado no catálogo de cursos.");
                    etapa.Status = StatusEtapaIntegracao.Pendente;
                    etapa.CursoTreinamentoId = c.CursoId;
                    etapa.CargaMinimaHoras = c.CargaMinimaHoras;
                    break;
                case StatusEtapaIntegracao.EmAndamento when etapa.SessaoTreinamentoId is { } sessaoId:
                    foreach (var trabalhadorId in c.Participantes)
                        _db.ParticipantesSessaoTreinamento.Add(new ParticipanteSessaoTreinamento { SessaoTreinamentoId = sessaoId, TrabalhadorId = trabalhadorId });
                    break;
            }
        }

        var funcaoIds = trabalhadores.Select(t => t.FuncaoId).Distinct().ToList();
        var funcoes = await _db.Funcoes.Where(f => funcaoIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Nome, ct);
        var planos = await _db.PlanosIntegracao
            .Where(p => p.ObraId == at.ObraId && p.Status == StatusPlanoIntegracao.Aberto && ids.Contains(p.TrabalhadorId))
            .Select(p => new { p.Id, p.TrabalhadorId }).ToListAsync(ct);
        foreach (var t in trabalhadores)
        {
            _db.ParticipantesIntegracao.Add(new ParticipanteIntegracao
            {
                ObraId = at.ObraId,
                IntegracaoAtendimentoId = at.Id,
                TrabalhadorId = t.Id,
                PlanoIntegracaoId = planos.FirstOrDefault(p => p.TrabalhadorId == t.Id)?.Id,
                FuncaoId = t.FuncaoId,
                FuncaoNome = funcoes.GetValueOrDefault(t.FuncaoId, string.Empty),
                Nome = t.Nome,
            });
        }
        at.Versao++;
        await _db.SaveChangesAsync(ct);
    }
}

public record RetirarParticipanteIntegracaoCommand(
    Guid IntegracaoId,
    Guid TrabalhadorId,
    string? AzureAdObjectId = null,
    bool AutenticacaoHabilitada = false) : IRequest;

public class RetirarParticipanteIntegracaoCommandHandler : IRequestHandler<RetirarParticipanteIntegracaoCommand>
{
    private readonly IAppDbContext _db;

    public RetirarParticipanteIntegracaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RetirarParticipanteIntegracaoCommand request, CancellationToken ct)
    {
        var (at, _) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        RegrasParticipantesIntegracao.ExigirEmAndamento(at);
        var etapas = await _db.IntegracaoEtapas.Where(e => e.IntegracaoAtendimentoId == at.Id).OrderBy(e => e.Ordem).ToListAsync(ct);
        await RegrasParticipantesIntegracao.ExigirPodeEditarAsync(_db, at, etapas, ct);

        var participante = await _db.ParticipantesIntegracao
            .FirstOrDefaultAsync(p => p.IntegracaoAtendimentoId == at.Id && p.TrabalhadorId == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Esta pessoa não participa desta integração.");
        if (await _db.ParticipantesIntegracao.CountAsync(p => p.IntegracaoAtendimentoId == at.Id, ct) <= 1)
            throw new InvalidOperationException("A integração precisa de ao menos uma pessoa.");

        var emAndamento = etapas.FirstOrDefault(e => e.Status == StatusEtapaIntegracao.EmAndamento && e.SessaoTreinamentoId != null);
        if (emAndamento?.SessaoTreinamentoId is { } sessaoId)
        {
            var naTurma = await _db.ParticipantesSessaoTreinamento
                .FirstOrDefaultAsync(p => p.SessaoTreinamentoId == sessaoId && p.TrabalhadorId == request.TrabalhadorId && p.Ativo, ct);
            if (naTurma is not null)
            {
                if (naTurma.PresencaConfirmadaEm is not null)
                    throw new InvalidOperationException("Esta pessoa já assinou a etapa e não pode mais sair da integração.");
                if (await _db.ParticipantesSessaoTreinamento.CountAsync(p => p.SessaoTreinamentoId == sessaoId && p.Ativo, ct) <= 1)
                    throw new InvalidOperationException("Esta é a única pessoa da etapa em andamento. Inclua outra antes de retirar esta.");
                _db.ParticipantesSessaoTreinamento.Remove(naTurma);
            }
        }

        _db.ParticipantesIntegracao.Remove(participante);
        at.Versao++;
        await _db.SaveChangesAsync(ct);
    }
}
