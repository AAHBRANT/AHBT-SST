using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes;

// Monta o payload publicado para o G-RH a cada criação/atualização/avanço de status de Acidente (ver
// IPublicadorAcidenteGrh). CPF vem via IgnoreQueryFilters porque um Trabalhador pode ter sido
// desativado (soft-delete ou Situacao=Desligado vindo do G-RH) sem que isso invalide o histórico do
// acidente que ele sofreu.
//
// Só funcionário com papel Envolvido entra no payload: testemunha e pessoa externa nunca vão ao G-RH.
public static class AcidenteGrhEventoFactory
{
    // Devolve null quando não há o que publicar. DECISÃO PROVISÓRIA (aprovada pelo usuário, a revisar com o
    // G-RH): ocorrência com pessoa externa (tipo 7) SEM funcionário envolvido não publica evento, porque o
    // G-RH trabalha por CPF de colaborador e a vítima não é colaboradora.
    public static async Task<AcidenteGrhEvento?> CriarAsync(IAppDbContext db, Acidente acidente, CancellationToken ct)
    {
        // Ordem de criação dos vínculos; o principal (TrabalhadorId) vai primeiro.
        var ids = await db.AcidentesEnvolvidos.AsNoTracking()
            .Where(e => e.AcidenteId == acidente.Id && e.Papel == PapelEnvolvido.Envolvido)
            .OrderBy(e => e.CreatedAtUtc)
            .Select(e => e.TrabalhadorId)
            .ToListAsync(ct);
        if (acidente.TrabalhadorId.HasValue)
        {
            ids.Remove(acidente.TrabalhadorId.Value);
            ids.Insert(0, acidente.TrabalhadorId.Value);
        }

        if (acidente.Tipo == TipoOcorrencia.OcorrenciaComPessoaExterna && ids.Count == 0)
            return null;

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
            // O G-RH não conhece "AguardandoLaudo" (é pausa interna): para ele a ocorrência segue "EmInvestigacao".
            (acidente.Status == StatusAcidente.AguardandoLaudo ? StatusAcidente.EmInvestigacao : acidente.Status).ToString(),
            cpfs);
    }

    // Monta e publica; sem evento (null) não chama o publicador.
    public static async Task PublicarAsync(IAppDbContext db, IPublicadorAcidenteGrh publicador, Acidente acidente, CancellationToken ct)
    {
        var evento = await CriarAsync(db, acidente, ct);
        if (evento is not null)
            await publicador.PublicarAsync(evento, ct);
    }
}
