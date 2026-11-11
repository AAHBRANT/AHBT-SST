using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

// Turma que é etapa de uma integração só se mexe pelo atendimento (decisão do usuário, 11/11/2026): presença, fotos e
// instrutor só valem com a etapa em andamento; encerrar, alterar a data e excluir pela tela comum de turma são
// recusados. Turma comum (IntegracaoAtendimentoId nulo) não é afetada.
public static class ProtecaoTurmaIntegracao
{
    public const string MensagemEtapaFechada =
        "Esta turma é uma etapa de integração e só aceita presença, fotos e instrutor enquanto a etapa está em andamento.";

    // Turma ligada a um atendimento? Ignora filtros: é só um sinalizador, não devolve dado da turma.
    public static async Task<Guid?> AtendimentoDaTurmaAsync(IAppDbContext db, Guid sessaoId, CancellationToken ct) =>
        await db.SessoesTreinamento.IgnoreQueryFilters()
            .Where(s => s.Id == sessaoId)
            .Select(s => s.IntegracaoAtendimentoId)
            .FirstOrDefaultAsync(ct);

    // Etapa da turma quando ela pertence a uma integração (null para turma comum).
    public static async Task<IntegracaoEtapa?> EtapaEmAndamentoDaTurmaAsync(IAppDbContext db, Guid sessaoId, CancellationToken ct)
    {
        if (await AtendimentoDaTurmaAsync(db, sessaoId, ct) is null) return null;

        var etapa = await db.IntegracaoEtapas.FirstOrDefaultAsync(e => e.SessaoTreinamentoId == sessaoId, ct);
        if (etapa is null || etapa.Status != StatusEtapaIntegracao.EmAndamento)
            throw new InvalidOperationException(MensagemEtapaFechada);
        var atendimentoAberto = await db.IntegracoesAtendimento
            .AnyAsync(a => a.Id == etapa.IntegracaoAtendimentoId && a.Status == StatusIntegracaoAtendimento.EmAndamento, ct);
        if (!atendimentoAberto)
            throw new InvalidOperationException(MensagemEtapaFechada);
        return etapa;
    }

    public static async Task GarantirEtapaEmAndamentoAsync(IAppDbContext db, Guid sessaoId, CancellationToken ct) =>
        _ = await EtapaEmAndamentoDaTurmaAsync(db, sessaoId, ct);

    public static async Task RecusarSeDeIntegracaoAsync(IAppDbContext db, Guid sessaoId, string acao, CancellationToken ct)
    {
        if (await AtendimentoDaTurmaAsync(db, sessaoId, ct) is not null)
            throw new InvalidOperationException(
                $"Esta turma é uma etapa de integração e não pode {acao} pela tela de turmas. Use \"Realizar integração\".");
    }
}
