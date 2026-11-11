using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ghes;

// As revisões recriam a estrutura do PGR para preservar os documentos históricos. Só transporta os
// detalhes da OS quando a origem e o destino são inequívocos e continuam com o mesmo conteúdo.
// Não usa o número do GHE ou o código EAP isoladamente: ambos podem ser reutilizados com outro sentido.
internal sealed class PreservacaoAtividadesOs
{
    private readonly IReadOnlyList<Ghe> _anteriores;
    private readonly ILookup<Guid, AtividadeFuncao> _decisoes;

    private PreservacaoAtividadesOs(IReadOnlyList<Ghe> anteriores, IEnumerable<AtividadeFuncao> decisoes)
    {
        _anteriores = anteriores;
        _decisoes = decisoes.ToLookup(x => x.AtividadeId);
    }

    public static async Task<PreservacaoAtividadesOs> CarregarAsync(IAppDbContext db, Guid obraId, CancellationToken ct)
    {
        // Snapshot sem tracking ANTES de desativar os GHEs e alterar o cadastro global das funções.
        var anteriores = await db.Ghes.AsNoTracking().Where(g => g.ObraId == obraId)
            .Include(g => g.Funcoes).ThenInclude(f => f.Funcao)
            .ToListAsync(ct);
        if (anteriores.Count == 0) return new PreservacaoAtividadesOs(anteriores, Array.Empty<AtividadeFuncao>());
        var gheIds = anteriores.Select(g => g.Id).ToList();
        var atividades = await db.Atividades.AsNoTracking()
            .Where(a => a.ObraId == obraId && a.GheId != null && gheIds.Contains(a.GheId.Value)).ToListAsync(ct);
        var ids = atividades.Select(a => a.Id).ToList();
        // Consultas separadas evitam multiplicar as linhas de documentos × EPCs × itens × riscos × medidas.
        var documentos = (await db.AtividadeDocumentos.AsNoTracking().Where(x => ids.Contains(x.AtividadeId)).ToListAsync(ct)).ToLookup(x => x.AtividadeId);
        var epcs = (await db.AtividadeEpcs.AsNoTracking().Where(x => ids.Contains(x.AtividadeId)).ToListAsync(ct)).ToLookup(x => x.AtividadeId);
        var itens = (await db.AtividadeItensOs.AsNoTracking().Where(x => ids.Contains(x.AtividadeId)).ToListAsync(ct)).ToLookup(x => x.AtividadeId);
        var riscos = await db.Riscos.AsNoTracking().Where(x => ids.Contains(x.AtividadeId)).Include(x => x.Perigo).ToListAsync(ct);
        var riscoIds = riscos.Select(r => r.Id).ToList();
        var medidas = (await db.MedidasPrevencao.AsNoTracking().Where(x => riscoIds.Contains(x.RiscoId)).ToListAsync(ct)).ToLookup(x => x.RiscoId);
        foreach (var risco in riscos) risco.Medidas = medidas[risco.Id].ToList();
        var riscosPorAtividade = riscos.ToLookup(r => r.AtividadeId);
        foreach (var atividade in atividades)
        {
            atividade.Documentos = documentos[atividade.Id].ToList();
            atividade.Epcs = epcs[atividade.Id].ToList();
            atividade.ItensOs = itens[atividade.Id].ToList();
            atividade.Riscos = riscosPorAtividade[atividade.Id].ToList();
        }
        var atividadesPorGhe = atividades.ToLookup(a => a.GheId);
        foreach (var ghe in anteriores) ghe.Atividades = atividadesPorGhe[ghe.Id].ToList();
        var decisoes = await db.AtividadesFuncoes.AsNoTracking()
            .Where(x => x.ObraId == obraId && ids.Contains(x.AtividadeId)).ToListAsync(ct);
        return new PreservacaoAtividadesOs(anteriores, decisoes);
    }

    public void Aplicar(IAppDbContext db, IReadOnlyList<Ghe> novos)
    {
        var porContexto = _anteriores.ToLookup(Contexto);
        var novosPorContexto = novos.ToLookup(Contexto);
        foreach (var novo in novos)
        {
            var contexto = Contexto(novo);
            if (porContexto[contexto].Count() != 1 || novosPorContexto[contexto].Count() != 1) continue;
            var anterior = porContexto[contexto].Single();
            var atividadesAnteriores = anterior.Atividades.ToLookup(IdentidadeAtividade);
            var atividadesNovas = novo.Atividades.ToLookup(IdentidadeAtividade);
            var funcoesAnteriores = anterior.Funcoes.ToLookup(IdentidadeFuncao);
            var funcoesNovas = novo.Funcoes.ToLookup(IdentidadeFuncao);

            foreach (var atividade in novo.Atividades)
            {
                var identidade = IdentidadeAtividade(atividade);
                if (atividadesAnteriores[identidade].Count() != 1 || atividadesNovas[identidade].Count() != 1) continue;
                var origem = atividadesAnteriores[identidade].Single();
                if (Inventario(origem) != Inventario(atividade)) continue;

                atividade.CondicoesExecucao = origem.CondicoesExecucao;
                atividade.Ferramentas = origem.Ferramentas;
                foreach (var documento in origem.Documentos)
                    db.AtividadeDocumentos.Add(new AtividadeDocumento
                    {
                        Atividade = atividade, MaterialApoioId = documento.MaterialApoioId,
                        Origem = documento.Origem, CreatedBy = documento.CreatedBy,
                    });
                foreach (var epc in origem.Epcs)
                    db.AtividadeEpcs.Add(new AtividadeEpc
                    {
                        Atividade = atividade, CatalogoEpcId = epc.CatalogoEpcId,
                        Origem = epc.Origem, CreatedBy = epc.CreatedBy,
                    });
                foreach (var item in origem.ItensOs)
                    db.AtividadeItensOs.Add(new AtividadeItemOs
                    {
                        Atividade = atividade, Tipo = item.Tipo, Texto = item.Texto, Requisito = item.Requisito,
                        OrigemItem = item.OrigemItem, Status = item.Status, Origem = item.Origem, CreatedBy = item.CreatedBy,
                        ValidadoPorUsuarioId = item.ValidadoPorUsuarioId, ValidadoPorNome = item.ValidadoPorNome,
                        ValidadoEmUtc = item.ValidadoEmUtc,
                    });

                foreach (var funcao in novo.Funcoes)
                {
                    var identidadeFuncao = IdentidadeFuncao(funcao);
                    if (funcoesAnteriores[identidadeFuncao].Count() != 1 || funcoesNovas[identidadeFuncao].Count() != 1) continue;
                    var funcaoId = funcao.Funcao?.Id ?? funcao.FuncaoId;
                    var decisoes = _decisoes[origem.Id].Where(d => d.FuncaoId == funcaoId).ToList();
                    if (decisoes.Count != 1) continue;
                    var decisao = decisoes[0];
                    db.AtividadesFuncoes.Add(new AtividadeFuncao
                    {
                        ObraId = atividade.ObraId, AtividadeId = atividade.Id, FuncaoId = funcaoId,
                        Status = decisao.Status, OrigemVinculo = decisao.OrigemVinculo,
                        JustificativaIa = decisao.JustificativaIa, Origem = decisao.Origem, CreatedBy = decisao.CreatedBy,
                        ValidadoPorUsuarioId = decisao.ValidadoPorUsuarioId, ValidadoPorNome = decisao.ValidadoPorNome,
                        ValidadoEmUtc = decisao.ValidadoEmUtc,
                    });
                }
            }
        }
    }

    private static string Texto(string? valor) => (valor ?? string.Empty).Trim();

    private static string Contexto(Ghe g) => JsonSerializer.Serialize(new
    {
        g.ObraId, g.Numero, Setor = Texto(g.Setor), Jornada = Texto(g.JornadaTrabalho),
        Ambiente = Texto(g.DescricaoAmbiente), Fonte = Texto(g.FonteGeradora), Medidas = Texto(g.MedidasProtecaoExistentes),
    });

    private static string IdentidadeAtividade(Atividade a) => JsonSerializer.Serialize(new
    {
        Nome = Texto(a.Nome), Descricao = Texto(a.Descricao),
    });

    private static string IdentidadeFuncao(GheFuncao f) => JsonSerializer.Serialize(new
    {
        FuncaoId = f.Funcao?.Id ?? f.FuncaoId, Descricao = Texto(f.DescricaoAtividades), Nome = Texto(f.Funcao?.Nome), Cbo = Texto(f.Funcao?.CboCodigo),
    });

    // Confirmações e procedimentos não são herdados se riscos ou medidas mudaram. Datas de avaliação
    // e IDs novos são ignorados; o conteúdo que o engenheiro analisou precisa continuar equivalente.
    private static string Inventario(Atividade a) => JsonSerializer.Serialize(a.Riscos.Select(r => JsonSerializer.Serialize(new
    {
        Perigo = Texto(r.Perigo?.Nome), Agente = Texto(r.Perigo?.Agente), Ambiente = Texto(r.Ambiente),
        Exposicao = Texto(r.Exposicao), Consequencia = Texto(r.Consequencia), r.Probabilidade, r.Severidade, r.NivelRisco,
        Existentes = Texto(r.ControlesExistentes), Adicionais = Texto(r.ControlesAdicionais),
        Medidas = r.Medidas.Select(m => JsonSerializer.Serialize(new { m.Fase, Texto = Texto(m.Texto), m.Status }))
            .OrderBy(m => m, StringComparer.Ordinal).ToArray(),
    })).OrderBy(r => r, StringComparer.Ordinal).ToArray());
}
