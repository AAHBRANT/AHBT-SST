using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// Inspeção periódica por item (24/10/2026, pedido do usuário): cada veículo ou item de tema com
// periodicidade (ex.: veículo a cada 30 dias) vence N dias depois da última inspeção concluída — ou
// do cadastro, se nunca foi inspecionado. O aviso vai ao responsável designado no próprio item, na
// obra dele; sem responsável, cai no responsável fixo do módulo Inspeção (RegraAlerta). Concluir uma
// inspeção empurra o vencimento e o motor encerra o alerta sozinho. Sem regra de antecedência
// cadastrada para o módulo Inspeção em Configurações, só o item vencido alerta (sempre Crítico).
public class InspecaoPeriodicaAlertaProvider : IAlertaOrigemProvider
{
    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.Inspecao;

    public InspecaoPeriodicaAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var veiculos = await _db.Veiculos.AsNoTracking()
            .Where(v => v.PeriodicidadeInspecaoDias != null)
            .Select(v => new { v.Id, v.ObraId, v.PlacaPrefixo, v.PeriodicidadeInspecaoDias, v.ResponsavelInspecaoUsuarioId, v.CreatedAtUtc })
            .ToListAsync(ct);
        var itens = await _db.ItensInspecionaveis.AsNoTracking()
            .Where(i => i.PeriodicidadeInspecaoDias != null)
            .Select(i => new
            {
                i.Id, i.ObraId, i.Identificacao, Tema = i.TemaInspecao!.Nome,
                i.PeriodicidadeInspecaoDias, i.ResponsavelInspecaoUsuarioId, i.CreatedAtUtc,
            })
            .ToListAsync(ct);

        var ultimas = await _db.Inspecoes.AsNoTracking()
            .Where(i => i.Status == StatusInspecao.Concluida && (i.VeiculoId != null || i.ItemInspecionavelId != null))
            .Select(i => new { i.VeiculoId, i.ItemInspecionavelId, i.Data })
            .ToListAsync(ct);
        var ultimaPorVeiculo = ultimas.Where(u => u.VeiculoId != null)
            .GroupBy(u => u.VeiculoId!.Value).ToDictionary(g => g.Key, g => g.Max(x => x.Data));
        var ultimaPorItem = ultimas.Where(u => u.ItemInspecionavelId != null)
            .GroupBy(u => u.ItemInspecionavelId!.Value).ToDictionary(g => g.Key, g => g.Max(x => x.Data));

        var resultado = new List<AlertaOrigemItem>();
        foreach (var v in veiculos)
        {
            var vencimento = Inspecoes.PeriodicidadeInspecao.ProximaEm(
                v.PeriodicidadeInspecaoDias, ultimaPorVeiculo.GetValueOrDefault(v.Id, default) is var u && u != default ? u : null, v.CreatedAtUtc)!.Value;
            resultado.Add(new AlertaOrigemItem
            {
                EntidadeOrigemTipo = nameof(Veiculo),
                EntidadeOrigemId = v.Id,
                DataVencimento = vencimento,
                TipoAlertaVencendo = TipoAlerta.InspecaoPendente,
                Titulo = $"Inspeção do veículo {v.PlacaPrefixo}",
                Descricao = $"Inspeção a cada {v.PeriodicidadeInspecaoDias} dia(s); vence em {vencimento:dd/MM/yyyy}.",
                ObraId = v.ObraId,
                DestinatarioUsuarioId = v.ResponsavelInspecaoUsuarioId,
            });
        }
        foreach (var i in itens)
        {
            var vencimento = Inspecoes.PeriodicidadeInspecao.ProximaEm(
                i.PeriodicidadeInspecaoDias, ultimaPorItem.GetValueOrDefault(i.Id, default) is var u && u != default ? u : null, i.CreatedAtUtc)!.Value;
            resultado.Add(new AlertaOrigemItem
            {
                EntidadeOrigemTipo = nameof(ItemInspecionavel),
                EntidadeOrigemId = i.Id,
                DataVencimento = vencimento,
                TipoAlertaVencendo = TipoAlerta.InspecaoPendente,
                Titulo = $"Inspeção de {i.Tema}: {i.Identificacao}",
                Descricao = $"Inspeção a cada {i.PeriodicidadeInspecaoDias} dia(s); vence em {vencimento:dd/MM/yyyy}.",
                ObraId = i.ObraId,
                DestinatarioUsuarioId = i.ResponsavelInspecaoUsuarioId,
            });
        }
        return resultado;
    }
}
