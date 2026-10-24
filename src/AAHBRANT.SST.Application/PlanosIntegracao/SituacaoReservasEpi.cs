using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PlanosIntegracao;

// Separado ou em falta: a reserva não grava isso, porque muda sozinho quando o estoque entra ou sai.
// As reservas ativas de cada (obra, EPI) formam uma fila na ordem em que foram criadas; enquanto a
// soma da fila couber no saldo físico, o item está separado — o que passa do saldo está em falta.
public static class SituacaoReservasEpi
{
    public static async Task<Dictionary<Guid, bool>> SeparadasAsync(
        IAppDbContext db, Guid obraId, IReadOnlyCollection<Guid> catalogoEpiIds, CancellationToken ct)
    {
        var saldos = await db.EstoquesEpi
            .Where(e => e.ObraId == obraId && catalogoEpiIds.Contains(e.CatalogoEpiId))
            .Select(e => new { e.CatalogoEpiId, e.Saldo })
            .ToDictionaryAsync(e => e.CatalogoEpiId, e => e.Saldo, ct);

        var fila = await db.ReservasEpi
            .Where(r => r.ObraId == obraId && catalogoEpiIds.Contains(r.CatalogoEpiId) && r.Status == StatusReservaEpi.Ativa)
            .OrderBy(r => r.CreatedAtUtc).ThenBy(r => r.Id)
            .Select(r => new { r.Id, r.CatalogoEpiId, r.Quantidade })
            .ToListAsync(ct);

        var resultado = new Dictionary<Guid, bool>();
        foreach (var grupo in fila.GroupBy(r => r.CatalogoEpiId))
        {
            var restante = saldos.GetValueOrDefault(grupo.Key);
            foreach (var reserva in grupo)
            {
                resultado[reserva.Id] = restante >= reserva.Quantidade;
                restante -= reserva.Quantidade;
            }
        }
        return resultado;
    }

    // Unidades já prometidas a kits de outros trabalhadores — o que uma entrega avulsa não pode usar.
    public static Task<int> ReservadoParaOutrosAsync(
        IAppDbContext db, Guid obraId, Guid catalogoEpiId, Guid trabalhadorId, CancellationToken ct) =>
        db.ReservasEpi
            .Where(r => r.ObraId == obraId && r.CatalogoEpiId == catalogoEpiId
                && r.Status == StatusReservaEpi.Ativa && r.TrabalhadorId != trabalhadorId)
            .SumAsync(r => r.Quantidade, ct);

    public static string MensagemSaldoComprometido(int saldo, int reservadoOutros) =>
        $"Estoque insuficiente para este EPI nesta obra: saldo {saldo}, dos quais {reservadoOutros} já estão separados para kits de funcionários novos (disponível: {Math.Max(0, saldo - reservadoOutros)}).";
}
