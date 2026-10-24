using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// Exames periódicos do PCMSO pela função do trabalhador (pedido de 09/10/2026, opção "ligar ao PCMSO").
// Um alerta por trabalhador, não por exame: com ~10 exames por função, um alerta por exame inundaria a
// tela e o Teams. O vencimento do alerta é o do exame mais urgente; a descrição lista os exames
// vencidos, vencendo ou nunca registrados.
//
// Vencimento de cada exame = última realização + periodicidade da função. A última realização vem de:
// 1) exame complementar com o código (ou nome) do exame do PCMSO;
// 2) registro antigo sem código cuja categoria identifica o exame sem ambiguidade (audiometria,
//    acuidade visual, espirometria) — "Laboratoriais" cobre glicemia, hemograma etc. e não serve;
// 3) para o exame clínico, também o último ASO (o exame clínico é o próprio ASO).
// Exame nunca registrado vence na data de admissão (deveria ter entrado no admissional).
public class ExamesFuncaoAlertaProvider : IAlertaOrigemProvider
{
    public const string EntidadeOrigemTipo = "ExamesFuncaoTrabalhador";

    // Janela da descrição: exames que vencem até aqui aparecem na lista (maior antecedência padrão do
    // RegraAlertaSeeder). O motor decide a severidade pelo exame mais urgente.
    private const int DiasJanelaDescricao = 30;

    private const string CodigoExameClinico = "0295";

    private static readonly Dictionary<string, TipoExameComplementar> CategoriaInequivoca = new()
    {
        ["0281"] = TipoExameComplementar.Audiometria,
        ["0296"] = TipoExameComplementar.AcuidadeVisual,
        ["1057"] = TipoExameComplementar.Espirometria,
    };

    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.ExamesFuncao;

    public ExamesFuncaoAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var exames = await _db.ExamesFuncaoObra.AsNoTracking()
            .Where(e => e.Periodico && e.PeriodicidadeMeses != null && e.PeriodicidadeMeses > 0)
            .Select(e => new { e.ObraId, e.FuncaoId, e.Exame, e.CodigoExame, Meses = e.PeriodicidadeMeses!.Value })
            .ToListAsync(ct);

        var itens = new List<AlertaOrigemItem>();
        if (exames.Count > 0)
        {
            var porObraFuncao = exames.ToLookup(e => (e.ObraId, e.FuncaoId));
            var obras = exames.Select(e => e.ObraId).Distinct().ToList();

            var trabalhadores = await _db.Trabalhadores.AsNoTracking()
                .Where(t => obras.Contains(t.ObraId))
                .Select(t => new { t.Id, t.Nome, t.ObraId, t.FuncaoId, t.DataAdmissao })
                .ToListAsync(ct);
            trabalhadores = trabalhadores.Where(t => porObraFuncao.Contains((t.ObraId, t.FuncaoId))).ToList();
            var ids = trabalhadores.Select(t => t.Id).ToList();

            var registros = (await _db.ExamesComplementares.AsNoTracking()
                    .Where(r => ids.Contains(r.TrabalhadorId))
                    .Select(r => new { r.TrabalhadorId, r.Tipo, r.CodigoExame, r.NomeExame, r.DataRealizacao })
                    .ToListAsync(ct))
                .ToLookup(r => r.TrabalhadorId);

            var ultimoAso = (await _db.Asos.AsNoTracking()
                    .Where(a => ids.Contains(a.TrabalhadorId))
                    .GroupBy(a => a.TrabalhadorId)
                    .Select(g => new { TrabalhadorId = g.Key, Data = g.Max(a => a.DataExame) })
                    .ToListAsync(ct))
                .ToDictionary(a => a.TrabalhadorId, a => a.Data);

            var hoje = DateTime.UtcNow.Date;
            foreach (var t in trabalhadores)
            {
                var situacoes = new List<(string Exame, DateTime Vence, bool NuncaRegistrado)>();
                foreach (var e in porObraFuncao[(t.ObraId, t.FuncaoId)])
                {
                    var nome = FuncaoSstClassifier.Normalizar(e.Exame);
                    var datas = registros[t.Id]
                        .Where(r =>
                            (e.CodigoExame != null && r.CodigoExame == e.CodigoExame)
                            || (r.NomeExame != null && FuncaoSstClassifier.Normalizar(r.NomeExame) == nome)
                            || (r.CodigoExame == null && e.CodigoExame != null
                                && CategoriaInequivoca.TryGetValue(e.CodigoExame, out var categoria) && r.Tipo == categoria))
                        .Select(r => r.DataRealizacao)
                        .ToList();
                    if ((e.CodigoExame == CodigoExameClinico || nome == "exame clinico") && ultimoAso.TryGetValue(t.Id, out var aso))
                        datas.Add(aso);

                    situacoes.Add(datas.Count == 0
                        ? (e.Exame, t.DataAdmissao.Date, true)
                        : (e.Exame, datas.Max().Date.AddMonths(e.Meses), false));
                }

                var maisUrgente = situacoes.Min(s => s.Vence);
                var listados = situacoes
                    .Where(s => (s.Vence - hoje).Days <= DiasJanelaDescricao)
                    .OrderBy(s => s.Vence).ThenBy(s => s.Exame)
                    .Select(s => s.NuncaRegistrado
                        ? $"• {s.Exame}: nunca registrado"
                        : $"• {s.Exame}: {((s.Vence - hoje).Days < 0 ? "venceu" : "vence")} em {s.Vence:dd/MM/yyyy}")
                    .ToList();

                itens.Add(new AlertaOrigemItem
                {
                    EntidadeOrigemTipo = EntidadeOrigemTipo,
                    EntidadeOrigemId = t.Id,
                    DataVencimento = maisUrgente,
                    TipoAlertaVencendo = TipoAlerta.ExameFuncaoVencendo,
                    TipoAlertaVencido = TipoAlerta.ExameFuncaoVencido,
                    Titulo = listados.Count == 1
                        ? $"Exame do PCMSO de {t.Nome}"
                        : $"{listados.Count} exames do PCMSO de {t.Nome}",
                    Descricao = listados.Count == 0 ? null : string.Join("\n", listados),
                    TrabalhadorId = t.Id,
                    ObraId = t.ObraId,
                });
            }
        }

        // Trabalhador desligado, que mudou de função/obra ou cuja função saiu do quadro: o motor só
        // encerra alerta de item que ele ainda recebe, então devolve o que ficou em aberto como substituído.
        var gerados = itens.Select(i => i.EntidadeOrigemId).ToHashSet();
        var emAberto = await _db.Alertas.AsNoTracking()
            .Where(a => a.EntidadeOrigemTipo == EntidadeOrigemTipo
                && (a.Status == StatusAlerta.Aberto || a.Status == StatusAlerta.EmTratamento || a.Status == StatusAlerta.Escalonado))
            .Select(a => a.EntidadeOrigemId)
            .ToListAsync(ct);
        itens.AddRange(emAberto.Where(id => !gerados.Contains(id)).Distinct().Select(id => new AlertaOrigemItem
        {
            EntidadeOrigemTipo = EntidadeOrigemTipo,
            EntidadeOrigemId = id,
            DataVencimento = DateTime.UtcNow.Date,
            TipoAlertaVencendo = TipoAlerta.ExameFuncaoVencendo,
            Substituido = true,
        }));

        return itens;
    }
}
