using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Higienizacao.Queries;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

public class HigienizacaoAlertaProvider : IAlertaOrigemProvider
{
    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.Higienizacao;

    public HigienizacaoAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var itens = await _db.ItensHigienizacao
            .Include(i => i.Obra)
            .Include(i => i.Registros)
            .ToListAsync(ct);

        // Reaproveita o mesmo cálculo de vencimento exibido na tela de Higienização (último
        // RegistroHigienizacao + PeriodicidadeDias, ou CreatedAtUtc se nunca higienizado) — não
        // duplica a regra aqui, para nunca ficar dessincronizado do que o usuário vê na lista.
        return itens.Select(item =>
        {
            var dto = ListarItensHigienizacaoQueryHandler.MapearParaDto(item);
            return new AlertaOrigemItem
            {
                EntidadeOrigemTipo = "ItemHigienizacao",
                EntidadeOrigemId = item.Id,
                DataVencimento = dto.ProximoVencimentoEm,
                TipoAlertaVencendo = TipoAlerta.HigienizacaoVencendo,
                TipoAlertaVencido = null,
                Titulo = $"Higienização de {item.Nome} — {item.Obra?.Nome ?? "obra"}",
                ObraId = item.ObraId,
            };
        }).ToList();
    }
}
