using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AcoesPlano.Commands;

// Conclusão manual com confirmação e Reabrir (23/10/2026, desenho aprovado em
// docs/mockups/plano-acao-conclusao-v1.html). Motivo: na tela da ocorrência o botão "Validar"
// concluía a ação num clique, sem perguntar, e o técnico validou um plano inteiro achando que era
// "passar para frente". Agora concluir exige descrever o que foi feito e registra quem concluiu;
// Reabrir desfaz enquanto a investigação estiver aberta.

public record ConcluirAcaoPlanoCommand(Guid Id, string DescricaoConclusao, Guid? UsuarioAtualId) : IRequest;

public record ReabrirAcaoPlanoCommand(Guid Id) : IRequest;

public class ConcluirAcaoPlanoCommandValidator : AbstractValidator<ConcluirAcaoPlanoCommand>
{
    public ConcluirAcaoPlanoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DescricaoConclusao).NotEmpty().WithMessage("Descreva o que foi feito para concluir a ação.").MaximumLength(1000);
    }
}

// Ações que o sistema conclui sozinho — não aceitam conclusão manual.
public static class ResolucaoAutomaticaAcaoPlano
{
    public static async Task<Dictionary<Guid, (string Tipo, string Texto)>> CarregarAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> acaoIds, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, (string, string)>();
        if (acaoIds.Count == 0) return resultado;

        var dds = await db.TemasDdsAgendados
            .Where(t => t.AcaoPlanoId != null && acaoIds.Contains(t.AcaoPlanoId.Value))
            .Select(t => new { AcaoId = t.AcaoPlanoId!.Value, t.Data })
            .ToListAsync(ct);
        foreach (var t in dds)
            resultado[t.AcaoId] = ("Dds", $"Ao encerrar o DDS de {t.Data:dd/MM} com este tema");

        var reunioes = await db.ReunioesAnaliseOcorrencia
            .Where(r => r.AcaoPlanoId != null && acaoIds.Contains(r.AcaoPlanoId.Value))
            .Select(r => r.AcaoPlanoId!.Value)
            .ToListAsync(ct);
        foreach (var id in reunioes)
            resultado[id] = ("Reuniao", "Ao registrar a reunião de análise como realizada");

        return resultado;
    }
}

internal static class AcaoPlanoOrigemAberta
{
    // Ocorrência com investigação concluída é registro fechado: o plano não muda mais.
    public static async Task GarantirAsync(IAppDbContext db, AcaoPlano acao, CancellationToken ct)
    {
        if (acao.OrigemTipo == nameof(Acidente)
            && await db.Acidentes.AnyAsync(a => a.Id == acao.OrigemId && a.Status == StatusAcidente.Concluido, ct))
            throw new InvalidOperationException("A investigação desta ocorrência já foi concluída; o plano de ação não pode mais ser alterado.");
    }
}

public class ConcluirAcaoPlanoCommandHandler : IRequestHandler<ConcluirAcaoPlanoCommand>
{
    private readonly IAppDbContext _db;

    public ConcluirAcaoPlanoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ConcluirAcaoPlanoCommand request, CancellationToken ct)
    {
        var acao = await _db.AcoesPlano.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Ação de plano {request.Id} não encontrada.");
        await _db.GarantirOrigemAcaoPlanoNoEscopoAsync(acao.OrigemTipo, acao.OrigemId, ct);
        await AcaoPlanoOrigemAberta.GarantirAsync(_db, acao, ct);

        if (acao.Status == StatusControleRisco.Concluido)
            throw new InvalidOperationException("Esta ação já está concluída.");

        var automatica = await ResolucaoAutomaticaAcaoPlano.CarregarAsync(_db, new[] { acao.Id }, ct);
        if (automatica.TryGetValue(acao.Id, out var resolucao))
            throw new InvalidOperationException($"Esta ação é concluída automaticamente: {resolucao.Texto.ToLowerInvariant()}.");

        if (acao.OrigemTipo == nameof(Acidente))
            throw new InvalidOperationException(acao.TipoResolucao is null
                ? "A conclusão manual não existe mais: defina como a ação se resolve (treinamento, inspeção ou entrega de EPI) em Alterar."
                : "Esta ação é concluída automaticamente pelo treinamento, inspeção ou entrega de EPI vinculado.");
        if (acao.TipoResolucao is not null)
            throw new InvalidOperationException("Esta ação é concluída automaticamente pelo treinamento, inspeção ou entrega de EPI vinculado.");

        acao.Status = StatusControleRisco.Concluido;
        acao.DataConclusao = DateTime.UtcNow;
        acao.ConcluidoPorUsuarioId = request.UsuarioAtualId;
        acao.DescricaoConclusao = request.DescricaoConclusao.Trim();
        await _db.SaveChangesAsync(ct);
    }
}

public class ReabrirAcaoPlanoCommandHandler : IRequestHandler<ReabrirAcaoPlanoCommand>
{
    private readonly IAppDbContext _db;

    public ReabrirAcaoPlanoCommandHandler(IAppDbContext db) => _db = db;

    // Vale para qualquer ação concluída, inclusive as automáticas e as "validadas" por engano: limpa
    // conclusão e validação e volta para Pendente.
    public async Task Handle(ReabrirAcaoPlanoCommand request, CancellationToken ct)
    {
        var acao = await _db.AcoesPlano.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Ação de plano {request.Id} não encontrada.");
        await _db.GarantirOrigemAcaoPlanoNoEscopoAsync(acao.OrigemTipo, acao.OrigemId, ct);
        await AcaoPlanoOrigemAberta.GarantirAsync(_db, acao, ct);

        if (acao.Status != StatusControleRisco.Concluido && acao.DataValidacao is null)
            throw new InvalidOperationException("Esta ação não está concluída.");

        acao.Status = StatusControleRisco.Pendente;
        acao.DataConclusao = null;
        acao.ConcluidoPorUsuarioId = null;
        acao.DescricaoConclusao = null;
        acao.DataValidacao = null;
        acao.ValidadoPorUsuarioId = null;
        await _db.SaveChangesAsync(ct);
    }
}
