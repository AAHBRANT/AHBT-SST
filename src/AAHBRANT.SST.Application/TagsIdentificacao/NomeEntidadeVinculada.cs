using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao;

// Nome legível do objeto de uma tag do adesivo de inspeção (Veículo = placa/prefixo, Item de tema = identificação,
// Alojamento = nome). Usa os filtros globais do contexto (escopo por obra do usuário): objeto fora do escopo ou
// excluído devolve null e a tela mostra "—" em vez do Id cru.
public static class NomeEntidadeVinculada
{
    public static bool EhObjetoDeInspecao(TipoEntidadeVinculada? tipo) =>
        tipo is TipoEntidadeVinculada.Veiculo or TipoEntidadeVinculada.ItemInspecionavel or TipoEntidadeVinculada.Alojamento
            or TipoEntidadeVinculada.Inspecao;

    public static async Task<string?> ObterAsync(IAppDbContext db, TipoEntidadeVinculada tipo, Guid id, CancellationToken ct)
    {
        var nomes = await ObterVariosAsync(db, tipo, new[] { id }, ct);
        return nomes.GetValueOrDefault(id);
    }

    public static async Task<Dictionary<Guid, string>> ObterVariosAsync(
        IAppDbContext db, TipoEntidadeVinculada tipo, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        return tipo switch
        {
            TipoEntidadeVinculada.Veiculo => await db.Veiculos.Where(v => ids.Contains(v.Id))
                .Select(v => new { v.Id, Nome = v.PlacaPrefixo }).ToDictionaryAsync(x => x.Id, x => x.Nome, ct),
            TipoEntidadeVinculada.ItemInspecionavel => await db.ItensInspecionaveis.Where(i => ids.Contains(i.Id))
                .Select(i => new { i.Id, Nome = i.Identificacao }).ToDictionaryAsync(x => x.Id, x => x.Nome, ct),
            TipoEntidadeVinculada.Alojamento => await db.Alojamentos.Where(a => ids.Contains(a.Id))
                .Select(a => new { a.Id, a.Nome }).ToDictionaryAsync(x => x.Id, x => x.Nome, ct),
            TipoEntidadeVinculada.Inspecao => await db.Inspecoes.Where(i => ids.Contains(i.Id))
                .Select(i => new { i.Id, Nome = i.NumeroDocumento ?? "Inspeção" }).ToDictionaryAsync(x => x.Id, x => x.Nome, ct),
            _ => new Dictionary<Guid, string>(),
        };
    }
}
