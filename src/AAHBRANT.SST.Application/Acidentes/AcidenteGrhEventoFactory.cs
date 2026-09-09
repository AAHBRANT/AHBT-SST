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
        string? cpf = null;
        if (acidente.TrabalhadorId.HasValue)
        {
            cpf = await db.Trabalhadores.IgnoreQueryFilters()
                .Where(t => t.Id == acidente.TrabalhadorId)
                .Select(t => t.Cpf)
                .FirstOrDefaultAsync(ct);
        }

        return new AcidenteGrhEvento(
            acidente.Id,
            cpf,
            acidente.Local,
            acidente.Data,
            acidente.Hora,
            acidente.NumeroCat,
            !string.IsNullOrWhiteSpace(acidente.NumeroCat),
            acidente.Status.ToString());
    }
}
