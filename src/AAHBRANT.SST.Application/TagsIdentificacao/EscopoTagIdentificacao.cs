using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao;

// A tag não tem ObraId: a obra é a da entidade vinculada (Trabalhador, Área ou Ativo). Auditoria de
// 09/10/2026: listar/obter/desvincular/excluir por id funcionava sobre a tag de qualquer obra.
//
// Regra: tag LIVRE (sem vínculo) é estoque comum e continua visível a todos; tag vinculada só é
// visível a quem tem acesso à obra da entidade. A obra da entidade é lida sem filtro global (só a
// obra conta, não o Ativo), para a tag de um trabalhador desligado continuar acessível à obra dele.
public static class EscopoTagIdentificacao
{
    public static IQueryable<TagIdentificacao> NoEscopoDaObra(this IQueryable<TagIdentificacao> query, IAppDbContext db)
    {
        if (db.EscopoObraGlobal)
            return query;

        var obras = db.ObrasNoEscopo;
        var trabalhadores = db.Trabalhadores.IgnoreQueryFilters().Where(t => obras.Contains(t.ObraId)).Select(t => t.Id);
        var areas = db.AreasSst.IgnoreQueryFilters().Where(a => obras.Contains(a.ObraId)).Select(a => a.Id);
        var ativos = db.AtivosSst.IgnoreQueryFilters().Where(a => obras.Contains(a.ObraId)).Select(a => a.Id);
        // Adesivo de inspeção: a tag QR do veículo, do item de tema e do alojamento segue a obra do objeto.
        var veiculos = db.Veiculos.IgnoreQueryFilters().Where(v => obras.Contains(v.ObraId)).Select(v => v.Id);
        var itens = db.ItensInspecionaveis.IgnoreQueryFilters().Where(i => obras.Contains(i.ObraId)).Select(i => i.Id);
        var alojamentos = db.Alojamentos.IgnoreQueryFilters().Where(a => obras.Contains(a.ObraId)).Select(a => a.Id);
        var inspecoes = db.Inspecoes.IgnoreQueryFilters().Where(i => obras.Contains(i.ObraId)).Select(i => i.Id);

        return query.Where(t =>
            t.EntidadeVinculadaTipo == null || t.EntidadeVinculadaId == null ||
            (t.EntidadeVinculadaTipo == TipoEntidadeVinculada.Trabalhador && trabalhadores.Contains(t.EntidadeVinculadaId.Value)) ||
            (t.EntidadeVinculadaTipo == TipoEntidadeVinculada.Area && areas.Contains(t.EntidadeVinculadaId.Value)) ||
            (t.EntidadeVinculadaTipo == TipoEntidadeVinculada.Ativo && ativos.Contains(t.EntidadeVinculadaId.Value)) ||
            (t.EntidadeVinculadaTipo == TipoEntidadeVinculada.Veiculo && veiculos.Contains(t.EntidadeVinculadaId.Value)) ||
            (t.EntidadeVinculadaTipo == TipoEntidadeVinculada.ItemInspecionavel && itens.Contains(t.EntidadeVinculadaId.Value)) ||
            (t.EntidadeVinculadaTipo == TipoEntidadeVinculada.Alojamento && alojamentos.Contains(t.EntidadeVinculadaId.Value)) ||
            (t.EntidadeVinculadaTipo == TipoEntidadeVinculada.Inspecao && inspecoes.Contains(t.EntidadeVinculadaId.Value)));
    }

    // Ao vincular: a entidade de destino também precisa ser de uma obra do usuário — senão um técnico
    // da obra A vincularia uma tag ao trabalhador da obra B.
    public static async Task GarantirEntidadeNoEscopoAsync(
        this IAppDbContext db, TipoEntidadeVinculada tipo, Guid entidadeId, CancellationToken ct)
    {
        if (db.EscopoObraGlobal)
            return;

        var obras = db.ObrasNoEscopo;
        var noEscopo = tipo switch
        {
            TipoEntidadeVinculada.Trabalhador => await db.Trabalhadores.IgnoreQueryFilters()
                .AnyAsync(t => t.Id == entidadeId && obras.Contains(t.ObraId), ct),
            TipoEntidadeVinculada.Area => await db.AreasSst.IgnoreQueryFilters()
                .AnyAsync(a => a.Id == entidadeId && obras.Contains(a.ObraId), ct),
            TipoEntidadeVinculada.Ativo => await db.AtivosSst.IgnoreQueryFilters()
                .AnyAsync(a => a.Id == entidadeId && obras.Contains(a.ObraId), ct),
            TipoEntidadeVinculada.Veiculo => await db.Veiculos.IgnoreQueryFilters()
                .AnyAsync(v => v.Id == entidadeId && obras.Contains(v.ObraId), ct),
            TipoEntidadeVinculada.ItemInspecionavel => await db.ItensInspecionaveis.IgnoreQueryFilters()
                .AnyAsync(i => i.Id == entidadeId && obras.Contains(i.ObraId), ct),
            TipoEntidadeVinculada.Alojamento => await db.Alojamentos.IgnoreQueryFilters()
                .AnyAsync(a => a.Id == entidadeId && obras.Contains(a.ObraId), ct),
            TipoEntidadeVinculada.Inspecao => await db.Inspecoes.IgnoreQueryFilters()
                .AnyAsync(i => i.Id == entidadeId && obras.Contains(i.ObraId), ct),
            _ => false,
        };

        if (!noEscopo)
            throw new KeyNotFoundException("Entidade a vincular não encontrada.");
    }
}
