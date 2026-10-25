using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AcoesPlano.Commands;

// Reabre de uma vez as ações do plano de uma ocorrência que foram concluídas SEM o fato que as
// resolve (24/10/2026, ocorrência do CAC): o técnico concluiu o plano inteiro à mão. Reabre:
// - ação sem vínculo (concluída à mão, o "Manual" que não existe mais);
// - ação vinculada (treinamento/inspeção/EPI) cujo fato ainda não aconteceu;
// - ação de DDS cujo DDS ainda não foi encerrado; ação de reunião cuja reunião ainda não foi realizada.
// Ação concluída pelo fato real fica como está. Devolve quantas reabriu.
public record ReabrirAcoesSemExecucaoCommand(Guid AcidenteId) : IRequest<int>;

public class ReabrirAcoesSemExecucaoCommandHandler : IRequestHandler<ReabrirAcoesSemExecucaoCommand, int>
{
    private readonly IAppDbContext _db;

    public ReabrirAcoesSemExecucaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(ReabrirAcoesSemExecucaoCommand request, CancellationToken ct)
    {
        await _db.GarantirOrigemAcaoPlanoNoEscopoAsync(nameof(Acidente), request.AcidenteId, ct);
        if (await _db.Acidentes.AnyAsync(a => a.Id == request.AcidenteId && a.Status == StatusAcidente.Concluido, ct))
            throw new InvalidOperationException("A investigação desta ocorrência já foi concluída; o plano de ação não pode mais ser alterado.");

        var concluidas = await _db.AcoesPlano
            .Where(a => a.OrigemTipo == nameof(Acidente) && a.OrigemId == request.AcidenteId
                && (a.Status == StatusControleRisco.Concluido || a.DataValidacao != null))
            .ToListAsync(ct);
        if (concluidas.Count == 0) return 0;

        var ids = concluidas.Select(a => a.Id).ToList();
        var ddsRealizado = (await _db.TemasDdsAgendados
                .Where(t => t.AcaoPlanoId != null && ids.Contains(t.AcaoPlanoId.Value))
                .Select(t => new { Id = t.AcaoPlanoId!.Value, Realizado = t.AplicadoEm != null })
                .ToListAsync(ct))
            .ToDictionary(t => t.Id, t => t.Realizado);
        var reuniaoRealizada = (await _db.ReunioesAnaliseOcorrencia
                .Where(r => r.AcaoPlanoId != null && ids.Contains(r.AcaoPlanoId.Value))
                .Select(r => new { Id = r.AcaoPlanoId!.Value, Realizada = r.RealizadaEm != null })
                .ToListAsync(ct))
            .ToDictionary(r => r.Id, r => r.Realizada);
        var vinculadas = await ResolucaoVinculadaAcaoPlano.AvaliarAsync(_db, new[] { request.AcidenteId }, ct);

        var reabertas = 0;
        foreach (var acao in concluidas)
        {
            bool temFatoReal;
            if (ddsRealizado.TryGetValue(acao.Id, out var dds)) temFatoReal = dds;
            else if (reuniaoRealizada.TryGetValue(acao.Id, out var reuniao)) temFatoReal = reuniao;
            else if (acao.TipoResolucao is not null) temFatoReal = vinculadas.TryGetValue(acao.Id, out var av) && av.Atendida;
            else temFatoReal = false; // sem vínculo: só podia ter sido concluída à mão

            if (temFatoReal) continue;

            acao.Status = StatusControleRisco.Pendente;
            acao.DataConclusao = null;
            acao.ConcluidoPorUsuarioId = null;
            acao.DescricaoConclusao = null;
            acao.DataValidacao = null;
            acao.ValidadoPorUsuarioId = null;
            reabertas++;
        }

        if (reabertas > 0) await _db.SaveChangesAsync(ct);
        return reabertas;
    }
}
