using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// Troca de função sem ASO de mudança de risco (12/11/2026). A NR-7 exige o exame quando a mudança
// altera os riscos ocupacionais, e antes da data da mudança (itens 7.5.6 "d" e 7.5.10). O sistema não
// compara os riscos das duas funções: só avisa o SESMT, que avalia e decide. NÃO bloqueia ninguém.
//
// Sai da lista (e o motor encerra o alerta) quando: entra um ASO de mudança de função para a troca;
// o trabalhador troca de função de novo (vale só a troca mais recente); ou o trabalhador é desligado.
// Alerta resolvido ou ignorado na tela (ex.: o SESMT concluiu que os riscos são os mesmos) não volta.
public class TrocaFuncaoSemAsoAlertaProvider : IAlertaOrigemProvider
{
    public const string EntidadeOrigemTipo = "TrocaFuncao";

    // O exame deve ser feito antes da mudança: um ASO de mudança feito até esta antecedência conta
    // como sendo desta troca. Limite definido no código, sem base em norma: evita que um ASO de
    // mudança antigo, de outra troca, dê esta por atendida.
    public const int DiasAntecedenciaAceitaDoAso = 90;

    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.TrocaFuncao;

    public TrocaFuncaoSemAsoAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        // O filtro global do Trabalhador já tira os desligados (Ativo = false).
        var trocas = await _db.HistoricosFuncaoTrabalhador.AsNoTracking()
            .Join(_db.Trabalhadores, h => h.TrabalhadorId, t => t.Id, (h, t) => new
            {
                h.Id, h.TrabalhadorId, h.AlteradaEm, h.FuncaoAnteriorId, h.FuncaoNovaId, t.Nome, t.ObraId,
            })
            .ToListAsync(ct);

        // Só a troca mais recente de cada trabalhador interessa.
        var ultimas = trocas.GroupBy(t => t.TrabalhadorId)
            .Select(g => g.OrderByDescending(t => t.AlteradaEm).First())
            .ToList();

        var idsTrabalhadores = ultimas.Select(t => t.TrabalhadorId).ToList();
        var asosMudanca = (await _db.Asos.AsNoTracking()
                .Where(a => idsTrabalhadores.Contains(a.TrabalhadorId) && a.Tipo == TipoExameAso.MudancaDeFuncao)
                .Select(a => new { a.TrabalhadorId, a.DataExame })
                .ToListAsync(ct))
            .ToLookup(a => a.TrabalhadorId, a => a.DataExame);

        var idsFuncoes = ultimas.SelectMany(t => new[] { t.FuncaoAnteriorId, t.FuncaoNovaId }).Distinct().ToList();
        var funcoes = await _db.Funcoes.AsNoTracking().IgnoreQueryFilters()
            .Where(f => idsFuncoes.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Nome, ct);

        var alertasDaOrigem = await _db.Alertas.AsNoTracking()
            .Where(a => a.EntidadeOrigemTipo == EntidadeOrigemTipo)
            .Select(a => new { a.EntidadeOrigemId, a.Status })
            .ToListAsync(ct);
        var jaEncerrados = alertasDaOrigem
            .Where(a => a.Status == StatusAlerta.Resolvido || a.Status == StatusAlerta.Ignorado)
            .Select(a => a.EntidadeOrigemId).ToHashSet();

        var itens = new List<AlertaOrigemItem>();
        foreach (var troca in ultimas)
        {
            if (jaEncerrados.Contains(troca.Id)) continue;

            var aPartirDe = troca.AlteradaEm.Date.AddDays(-DiasAntecedenciaAceitaDoAso);
            if (asosMudanca[troca.TrabalhadorId].Any(dataExame => dataExame.Date >= aPartirDe)) continue;

            var anterior = funcoes.GetValueOrDefault(troca.FuncaoAnteriorId) ?? "função anterior";
            var nova = funcoes.GetValueOrDefault(troca.FuncaoNovaId) ?? "função nova";
            itens.Add(new AlertaOrigemItem
            {
                EntidadeOrigemTipo = EntidadeOrigemTipo,
                EntidadeOrigemId = troca.Id,
                // O exame deveria existir antes da troca: o item vence na data dela.
                DataVencimento = troca.AlteradaEm.Date,
                TipoAlertaVencendo = TipoAlerta.TrocaFuncaoSemAsoMudancaRisco,
                TipoAlertaVencido = TipoAlerta.TrocaFuncaoSemAsoMudancaRisco,
                Titulo = $"ASO de mudança de risco de {troca.Nome}: trocou de função em {troca.AlteradaEm:dd/MM/yyyy}",
                Descricao = $"De {anterior} para {nova}, sem ASO de mudança de risco lançado. "
                    + "Se a função nova muda os riscos ocupacionais, a NR-7 exige o exame antes da mudança (item 7.5.10). "
                    + "Avalie com o médico do PCMSO: lance o ASO de mudança de função ou, se os riscos forem os mesmos, encerre este alerta.",
                TrabalhadorId = troca.TrabalhadorId,
                ObraId = troca.ObraId,
            });
        }

        // Quem saiu da lista (ASO lançado, nova troca, desligamento): o motor só encerra alerta de item
        // que ainda recebe, então devolve o que ficou em aberto como substituído.
        var gerados = itens.Select(i => i.EntidadeOrigemId).ToHashSet();
        var emAberto = alertasDaOrigem
            .Where(a => a.Status == StatusAlerta.Aberto || a.Status == StatusAlerta.EmTratamento || a.Status == StatusAlerta.Escalonado)
            .Select(a => a.EntidadeOrigemId);
        itens.AddRange(emAberto.Where(id => !gerados.Contains(id)).Distinct().Select(id => new AlertaOrigemItem
        {
            EntidadeOrigemTipo = EntidadeOrigemTipo,
            EntidadeOrigemId = id,
            DataVencimento = DateTime.UtcNow.Date,
            TipoAlertaVencendo = TipoAlerta.TrocaFuncaoSemAsoMudancaRisco,
            Substituido = true,
        }));

        return itens;
    }
}
