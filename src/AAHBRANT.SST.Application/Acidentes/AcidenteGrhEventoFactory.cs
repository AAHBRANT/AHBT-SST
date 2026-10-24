using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes;

// Monta o payload publicado para o G-RH a cada criação/atualização/avanço de status de Acidente (ver
// IPublicadorAcidenteGrh). CPF vem via IgnoreQueryFilters porque um Trabalhador pode ter sido
// desativado (soft-delete ou Situacao=Desligado vindo do G-RH) sem que isso invalide o histórico do
// acidente que ele sofreu.
public static class AcidenteGrhEventoFactory
{
    public static async Task<AcidenteGrhEvento> CriarAsync(IAppDbContext db, Acidente acidente, CancellationToken ct)
    {
        // Ordem de criação dos vínculos; o principal (TrabalhadorId) vai primeiro.
        var ids = await db.AcidentesEnvolvidos.AsNoTracking()
            .Where(e => e.AcidenteId == acidente.Id)
            .OrderBy(e => e.CreatedAtUtc)
            .Select(e => e.TrabalhadorId)
            .ToListAsync(ct);
        if (acidente.TrabalhadorId.HasValue)
        {
            ids.Remove(acidente.TrabalhadorId.Value);
            ids.Insert(0, acidente.TrabalhadorId.Value);
        }

        var cpfPorId = await db.Trabalhadores.IgnoreQueryFilters().AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, t.Cpf })
            .ToDictionaryAsync(t => t.Id, t => t.Cpf, ct);
        var cpfs = ids.Select(i => cpfPorId.GetValueOrDefault(i))
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).ToList();
        var cpf = acidente.TrabalhadorId.HasValue ? cpfPorId.GetValueOrDefault(acidente.TrabalhadorId.Value) : null;

        return new AcidenteGrhEvento(
            acidente.Id,
            cpf,
            acidente.Local,
            acidente.Data,
            acidente.Hora,
            acidente.NumeroCat,
            !string.IsNullOrWhiteSpace(acidente.NumeroCat),
            acidente.Status.ToString(),
            cpfs);
    }
}
