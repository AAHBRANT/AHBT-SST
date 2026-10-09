using System.Linq.Expressions;
using AAHBRANT.SST.Application.Common.Interfaces;
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

    public static bool ObraNoEscopo(this IAppDbContext db, Guid? obraId) =>
        db.EscopoObraGlobal || (obraId.HasValue && db.ObrasNoEscopo.Contains(obraId.Value));
}
