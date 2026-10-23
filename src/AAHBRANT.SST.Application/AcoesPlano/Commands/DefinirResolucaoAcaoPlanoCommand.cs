using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AcoesPlano.Commands;

// "Como se resolve" de uma ação já existente (Alterar, na tela da ocorrência): Manual (tudo nulo) ou
// vinculada a treinamento/inspeção/EPI. Se o fato já aconteceu, a ação conclui na hora.
public record DefinirResolucaoAcaoPlanoCommand(
    Guid Id,
    TipoResolucaoAcaoPlano? TipoResolucao,
    Guid? CursoTreinamentoId,
    TipoInspecao? TipoInspecao,
    Guid? CatalogoEpiId) : IRequest;

public static class VinculoResolucaoAcaoPlano
{
    // Confere que o alvo do tipo escolhido veio preenchido e existe, e aplica na ação (zerando os outros).
    public static async Task AplicarAsync(IAppDbContext db, AcaoPlano acao, TipoResolucaoAcaoPlano? tipo,
        Guid? cursoId, TipoInspecao? tipoInspecao, Guid? epiId, CancellationToken ct)
    {
        switch (tipo)
        {
            case TipoResolucaoAcaoPlano.Treinamento:
                if (cursoId is null || !await db.CursosTreinamento.AnyAsync(c => c.Id == cursoId, ct))
                    throw new InvalidOperationException("Escolha o curso do treinamento que resolve esta ação.");
                break;
            case TipoResolucaoAcaoPlano.Inspecao:
                if (tipoInspecao is null)
                    throw new InvalidOperationException("Escolha o tipo de inspeção que resolve esta ação.");
                break;
            case TipoResolucaoAcaoPlano.EntregaEpi:
                if (epiId is null || !await db.CatalogoEpis.AnyAsync(c => c.Id == epiId, ct))
                    throw new InvalidOperationException("Escolha o EPI cuja entrega resolve esta ação.");
                break;
        }

        acao.TipoResolucao = tipo;
        acao.ResolucaoCursoTreinamentoId = tipo == TipoResolucaoAcaoPlano.Treinamento ? cursoId : null;
        acao.ResolucaoTipoInspecao = tipo == TipoResolucaoAcaoPlano.Inspecao ? tipoInspecao : null;
        acao.ResolucaoCatalogoEpiId = tipo == TipoResolucaoAcaoPlano.EntregaEpi ? epiId : null;
    }
}

public class DefinirResolucaoAcaoPlanoCommandHandler : IRequestHandler<DefinirResolucaoAcaoPlanoCommand>
{
    private readonly IAppDbContext _db;

    public DefinirResolucaoAcaoPlanoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirResolucaoAcaoPlanoCommand request, CancellationToken ct)
    {
        var acao = await _db.AcoesPlano.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Ação de plano {request.Id} não encontrada.");
        await _db.GarantirOrigemAcaoPlanoNoEscopoAsync(acao.OrigemTipo, acao.OrigemId, ct);
        await AcaoPlanoOrigemAberta.GarantirAsync(_db, acao, ct);

        if (acao.Status == StatusControleRisco.Concluido)
            throw new InvalidOperationException("Esta ação já está concluída. Reabra a ação para mudar como ela se resolve.");
        if ((await ResolucaoAutomaticaAcaoPlano.CarregarAsync(_db, new[] { acao.Id }, ct)).ContainsKey(acao.Id))
            throw new InvalidOperationException("Esta ação já se resolve pelo DDS ou pela reunião de análise.");

        await VinculoResolucaoAcaoPlano.AplicarAsync(_db, acao, request.TipoResolucao, request.CursoTreinamentoId,
            request.TipoInspecao, request.CatalogoEpiId, ct);
        await _db.SaveChangesAsync(ct);

        if (acao.OrigemTipo == nameof(Acidente))
            await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(_db, new[] { acao.OrigemId }, ct);
    }
}
