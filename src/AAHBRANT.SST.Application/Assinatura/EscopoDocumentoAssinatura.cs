using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

// Escopo por obra do Motor de Assinatura. DocumentoAssinatura é polimórfico (EntidadeTipo +
// EntidadeId) e não tem ObraId próprio, então o filtro global não alcança: até a auditoria de
// 09/10/2026, GET /assinatura/listar devolvia documentos (com token público) de todas as obras, e o
// PDF assinado de qualquer obra podia ser baixado pelo id. Aqui cada tipo é resolvido até a obra da
// entidade de origem.
//
// Todas as consultas usam IgnoreQueryFilters e comparam SÓ a obra: documento de DDS/entrega de um
// registro excluído ou de trabalhador desligado continua visível para quem é daquela obra.
// Tipo desconhecido (novo tipo sem entrada aqui) fica invisível para usuário restrito — falha
// fechada; quem tem acesso global continua vendo tudo.
public static class EscopoDocumentoAssinatura
{
    public static async Task<bool> PodeVerAsync(IAppDbContext db, string entidadeTipo, Guid entidadeId, CancellationToken ct)
    {
        if (db.EscopoObraGlobal)
            return true;

        var visiveis = await EntidadesNoEscopoAsync(db, entidadeTipo, new[] { entidadeId }, ct);
        return visiveis.Contains(entidadeId);
    }

    // Filtra uma lista já materializada (o painel lista resumos, sem o PDF), uma consulta por tipo.
    public static async Task<List<T>> FiltrarAsync<T>(
        IAppDbContext db, List<T> documentos, Func<T, string> entidadeTipo, Func<T, Guid> entidadeId, CancellationToken ct)
    {
        if (db.EscopoObraGlobal || documentos.Count == 0)
            return documentos;

        var visiveisPorTipo = new Dictionary<string, HashSet<Guid>>(StringComparer.OrdinalIgnoreCase);
        foreach (var grupo in documentos.GroupBy(entidadeTipo, StringComparer.OrdinalIgnoreCase))
            visiveisPorTipo[grupo.Key] = await EntidadesNoEscopoAsync(db, grupo.Key, grupo.Select(entidadeId).Distinct().ToList(), ct);

        return documentos
            .Where(d => visiveisPorTipo[entidadeTipo(d)].Contains(entidadeId(d)))
            .ToList();
    }

    private static async Task<HashSet<Guid>> EntidadesNoEscopoAsync(
        IAppDbContext db, string entidadeTipo, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (db.ObrasNoEscopo.Count == 0)
            return new HashSet<Guid>();

        var consulta = ConsultaEntidadesNoEscopo(db, entidadeTipo, ids);
        return consulta is null
            ? new HashSet<Guid>()
            : (await consulta.ToListAsync(ct)).ToHashSet();
    }

    // Público só para o teste de tradução SQL (o InMemory dos testes não pega consulta que o SQL
    // Server não sabe traduzir — ver EscopoObraTraducaoSqlTests).
    public static IQueryable<Guid>? ConsultaEntidadesNoEscopo(IAppDbContext db, string entidadeTipo, IReadOnlyCollection<Guid> ids)
    {
        var obras = db.ObrasNoEscopo;
        var trabalhadores = db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => obras.Contains(t.ObraId)).Select(t => t.Id);
        var atividades = db.Atividades.IgnoreQueryFilters()
            .Where(a => obras.Contains(a.ObraId)).Select(a => a.Id);

        return entidadeTipo.ToLowerInvariant() switch
        {
            "dds" => db.Dds.IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id) && obras.Contains(x.ObraId)).Select(x => x.Id),
            "ddssemanal" => db.DdsSemanais.IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id) && obras.Contains(x.ObraId)).Select(x => x.Id),
            "inspecao" => db.Inspecoes.IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id) && obras.Contains(x.ObraId)).Select(x => x.Id),
            "sessaotreinamento" => db.SessoesTreinamento.IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id) && obras.Contains(x.ObraId)).Select(x => x.Id),
            "processoeleitoralcipa" => db.ProcessosEleitoraisCipa.IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id) && obras.Contains(x.ObraId)).Select(x => x.Id),
            "reuniaocipa" => db.ReunioesCipa.IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id) && obras.Contains(x.ObraId)).Select(x => x.Id),
            "apr" => db.Aprs.IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id) && atividades.Contains(x.AtividadeId)).Select(x => x.Id),
            "permissaotrabalho" => db.PermissoesTrabalho.IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id) && atividades.Contains(x.AtividadeId)).Select(x => x.Id),
            // NC chega à obra pela atividade, pelo risco (→ atividade) ou pelo item de inspeção (→ inspeção).
            "naoconformidade" => db.NaoConformidades.IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id) && (
                    (x.AtividadeId.HasValue && atividades.Contains(x.AtividadeId.Value))
                    || (x.RiscoId.HasValue && db.Riscos.IgnoreQueryFilters()
                        .Any(r => r.Id == x.RiscoId.Value && atividades.Contains(r.AtividadeId)))
                    || (x.InspecaoItemRespostaId.HasValue && db.InspecaoItemRespostas.IgnoreQueryFilters()
                        .Any(r => r.Id == x.InspecaoItemRespostaId.Value && db.Inspecoes.IgnoreQueryFilters()
                            .Any(i => i.Id == r.InspecaoId && obras.Contains(i.ObraId))))))
                .Select(x => x.Id),
            "treinamento" => db.Treinamentos.IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id) && trabalhadores.Contains(x.TrabalhadorId)).Select(x => x.Id),
            // DevolucaoEpi usa o id da própria entrega (ver ExportarFichaEpiTrabalhadorQuery).
            "entregaepi" or "devolucaoepi" => db.EntregasEpi.IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id) && trabalhadores.Contains(x.TrabalhadorId)).Select(x => x.Id),
            "entregauniforme" => db.EntregasUniforme.IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id) && trabalhadores.Contains(x.TrabalhadorId)).Select(x => x.Id),
            // Ficha de EPI e Termo de Compromisso: EntidadeId é o próprio Trabalhador.
            "fichaepitrabalhador" or "termocompromissoepi" => trabalhadores.Where(t => ids.Contains(t)),
            _ => null,
        };
    }
}
