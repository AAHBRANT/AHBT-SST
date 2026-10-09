using System.Linq.Expressions;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Common.Seguranca;

// Escopo por obra para entidades que chegam à obra só pelo Trabalhador e por isso ficaram FORA do
// filtro global do SstDbContext (Aso, ExameComplementar, Aptidão, Treinamento, EntregaEpi...).
// Auditoria de 09/10/2026 (antes de levar o SST às demais obras): nenhum handler dessas entidades
// conferia a obra — um técnico da Obra A listava, editava e excluía ASO da Obra B.
//
// Por que não pela navegação (a.Trabalhador.ObraId): o EF aplica o filtro global de Trabalhador
// (Ativo) no JOIN, e o registro de trabalhador desligado some — foi exatamente por isso que o filtro
// global dessas entidades foi revertido. Aqui o subquery usa IgnoreQueryFilters e olha SÓ a obra,
// então o histórico de desligados continua visível para quem é da obra dele.
//
// Acesso negado vira KeyNotFoundException (404), não 403: não confirma a quem é de fora que o id existe.
public static class EscopoObra
{
    // Ids de trabalhadores (inclusive desligados/excluídos) das obras visíveis ao usuário atual.
    // Só faz sentido quando o escopo não é global — chamadores devem checar EscopoObraGlobal antes.
    private static IQueryable<Guid> IdsTrabalhadoresNoEscopo(IAppDbContext db)
    {
        var obras = db.ObrasNoEscopo;
        return db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => obras.Contains(t.ObraId))
            .Select(t => t.Id);
    }

    public static IQueryable<T> NoEscopoDoTrabalhador<T>(
        this IQueryable<T> query, IAppDbContext db, Expression<Func<T, Guid>> trabalhadorId)
    {
        if (db.EscopoObraGlobal)
            return query;

        var ids = IdsTrabalhadoresNoEscopo(db);
        var contem = Expression.Call(
            typeof(Queryable), nameof(Queryable.Contains), new[] { typeof(Guid) },
            ids.Expression, trabalhadorId.Body);
        return query.Where(Expression.Lambda<Func<T, bool>>(contem, trabalhadorId.Parameters));
    }

    public static async Task<bool> TrabalhadorNoEscopoAsync(this IAppDbContext db, Guid trabalhadorId, CancellationToken ct)
    {
        if (db.EscopoObraGlobal)
            return true;

        var obras = db.ObrasNoEscopo;
        return await db.Trabalhadores.IgnoreQueryFilters()
            .AnyAsync(t => t.Id == trabalhadorId && obras.Contains(t.ObraId), ct);
    }

    public static async Task GarantirTrabalhadorNoEscopoAsync(this IAppDbContext db, Guid trabalhadorId, CancellationToken ct)
    {
        if (!await db.TrabalhadorNoEscopoAsync(trabalhadorId, ct))
            throw new KeyNotFoundException("Trabalhador não encontrado.");
    }

    // APR, PT e NC chegam à obra pela Atividade. A leitura de APR/PT já ficava protegida por acaso
    // (Include da Atividade filtrada vira INNER JOIN), mas a escrita por id não: um técnico da obra A
    // autorizava/encerrava a PT da obra B. Mesmo critério do Trabalhador: só a obra, sem o Ativo.
    public static IQueryable<T> NoEscopoDaAtividade<T>(
        this IQueryable<T> query, IAppDbContext db, Expression<Func<T, Guid>> atividadeId)
    {
        if (db.EscopoObraGlobal)
            return query;

        var obras = db.ObrasNoEscopo;
        var ids = db.Atividades.IgnoreQueryFilters()
            .Where(a => obras.Contains(a.ObraId))
            .Select(a => a.Id);
        var contem = Expression.Call(
            typeof(Queryable), nameof(Queryable.Contains), new[] { typeof(Guid) },
            ids.Expression, atividadeId.Body);
        return query.Where(Expression.Lambda<Func<T, bool>>(contem, atividadeId.Parameters));
    }

    public static async Task GarantirAtividadeNoEscopoAsync(this IAppDbContext db, Guid atividadeId, CancellationToken ct)
    {
        if (db.EscopoObraGlobal)
            return;

        var obras = db.ObrasNoEscopo;
        if (!await db.Atividades.IgnoreQueryFilters().AnyAsync(a => a.Id == atividadeId && obras.Contains(a.ObraId), ct))
            throw new KeyNotFoundException("Atividade não encontrada.");
    }

    public static async Task GarantirAprNoEscopoAsync(this IAppDbContext db, Guid aprId, CancellationToken ct)
    {
        if (!await db.Aprs.NoEscopoDaAtividade(db, a => a.AtividadeId).AnyAsync(a => a.Id == aprId, ct))
            throw new KeyNotFoundException($"APR {aprId} não encontrada.");
    }

    public static async Task GarantirPermissaoTrabalhoNoEscopoAsync(this IAppDbContext db, Guid permissaoTrabalhoId, CancellationToken ct)
    {
        if (!await db.PermissoesTrabalho.NoEscopoDaAtividade(db, p => p.AtividadeId).AnyAsync(p => p.Id == permissaoTrabalhoId, ct))
            throw new KeyNotFoundException($"Permissão de Trabalho {permissaoTrabalhoId} não encontrada.");
    }

    // NC chega à obra pela atividade, pelo risco (→ atividade) ou pelo item de inspeção (→ inspeção).
    // NC sem nenhum desses vínculos não tem obra: continua visível como antes (senão sumiria de quem
    // a registrou), até a NC ganhar ObraId próprio.
    public static IQueryable<NaoConformidade> NoEscopoDaObra(this IQueryable<NaoConformidade> query, IAppDbContext db)
    {
        if (db.EscopoObraGlobal)
            return query;

        var obras = db.ObrasNoEscopo;
        var atividades = db.Atividades.IgnoreQueryFilters().Where(a => obras.Contains(a.ObraId)).Select(a => a.Id);
        var riscos = db.Riscos.IgnoreQueryFilters().Where(r => atividades.Contains(r.AtividadeId)).Select(r => r.Id);
        var inspecoes = db.Inspecoes.IgnoreQueryFilters().Where(i => obras.Contains(i.ObraId)).Select(i => i.Id);
        var respostas = db.InspecaoItemRespostas.IgnoreQueryFilters().Where(r => inspecoes.Contains(r.InspecaoId)).Select(r => r.Id);

        return query.Where(n =>
            (n.AtividadeId == null && n.RiscoId == null && n.InspecaoItemRespostaId == null)
            || (n.AtividadeId != null && atividades.Contains(n.AtividadeId.Value))
            || (n.RiscoId != null && riscos.Contains(n.RiscoId.Value))
            || (n.InspecaoItemRespostaId != null && respostas.Contains(n.InspecaoItemRespostaId.Value)));
    }

    public static async Task GarantirNaoConformidadeNoEscopoAsync(this IAppDbContext db, Guid naoConformidadeId, CancellationToken ct)
    {
        if (!await db.NaoConformidades.NoEscopoDaObra(db).AnyAsync(n => n.Id == naoConformidadeId, ct))
            throw new KeyNotFoundException($"Não conformidade {naoConformidadeId} não encontrada.");
    }

    public static async Task GarantirRiscoNoEscopoAsync(this IAppDbContext db, Guid riscoId, CancellationToken ct)
    {
        if (!await db.Riscos.NoEscopoDaAtividade(db, r => r.AtividadeId).AnyAsync(r => r.Id == riscoId, ct))
            throw new KeyNotFoundException($"Risco {riscoId} não encontrado.");
    }

    // Equipe chega à obra pelo Setor. A listagem lia Setor/Obra com IgnoreQueryFilters (para mostrar
    // o nome da obra) e por isso devolvia as equipes de todas as obras.
    public static IQueryable<Equipe> NoEscopoDaObra(this IQueryable<Equipe> query, IAppDbContext db)
    {
        if (db.EscopoObraGlobal)
            return query;

        var obras = db.ObrasNoEscopo;
        var setores = db.Setores.IgnoreQueryFilters().Where(s => obras.Contains(s.ObraId)).Select(s => s.Id);
        return query.Where(e => setores.Contains(e.SetorId));
    }

    // Plano de ação do PGR (PlanoAcaoItem) chega à obra pelo PGR.
    public static IQueryable<PlanoAcaoItem> NoEscopoDaObra(this IQueryable<PlanoAcaoItem> query, IAppDbContext db)
    {
        if (db.EscopoObraGlobal)
            return query;

        var obras = db.ObrasNoEscopo;
        var pgrs = db.Pgrs.IgnoreQueryFilters().Where(p => obras.Contains(p.ObraId)).Select(p => p.Id);
        return query.Where(i => pgrs.Contains(i.PgrId));
    }

    public static async Task GarantirPgrNoEscopoAsync(this IAppDbContext db, Guid pgrId, CancellationToken ct)
    {
        if (db.EscopoObraGlobal)
            return;

        var obras = db.ObrasNoEscopo;
        if (!await db.Pgrs.IgnoreQueryFilters().AnyAsync(p => p.Id == pgrId && obras.Contains(p.ObraId), ct))
            throw new KeyNotFoundException($"PGR {pgrId} não encontrado.");
    }

    // AcaoPlano é polimórfica (OrigemTipo/OrigemId). Origens em uso: Não Conformidade, Acidente,
    // Reunião da CIPA e PCMSO. Origem desconhecida fica fechada para usuário restrito.
    public static async Task GarantirOrigemAcaoPlanoNoEscopoAsync(this IAppDbContext db, string origemTipo, Guid origemId, CancellationToken ct)
    {
        if (db.EscopoObraGlobal)
            return;

        var obras = db.ObrasNoEscopo;
        var permitido = origemTipo switch
        {
            nameof(NaoConformidade) => await db.NaoConformidades.NoEscopoDaObra(db).AnyAsync(n => n.Id == origemId, ct),
            nameof(Acidente) => await db.Acidentes.IgnoreQueryFilters().AnyAsync(a => a.Id == origemId && obras.Contains(a.ObraId), ct),
            nameof(ReuniaoCipa) => await db.ReunioesCipa.IgnoreQueryFilters().AnyAsync(r => r.Id == origemId && obras.Contains(r.ObraId), ct),
            "Pcmso" => await db.PcmsoDetalhes.IgnoreQueryFilters()
                .AnyAsync(p => p.Id == origemId && p.ObraId != null && obras.Contains(p.ObraId.Value), ct),
            _ => false,
        };
        if (!permitido)
            throw new KeyNotFoundException("Origem do plano de ação não encontrada.");
    }

    public static bool ObraNoEscopo(this IAppDbContext db, Guid? obraId) =>
        db.EscopoObraGlobal || (obraId.HasValue && db.ObrasNoEscopo.Contains(obraId.Value));
}
