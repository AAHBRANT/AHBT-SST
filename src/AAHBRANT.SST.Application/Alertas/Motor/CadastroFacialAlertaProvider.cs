using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// Cadastro facial com falhas repetidas de reconhecimento (3 ou mais em 30 dias contra o cadastro atual, a
// mesma regra do card "Cadastros faciais para revisar"): avisa para refazer a foto antes de a pessoa travar
// no próximo DDS (12/10). Não é vencimento de verdade: o item já nasce "vencido" na data da última falha.
// Quando o cadastro é refeito (ou as falhas param), o alerta é encerrado.
public class CadastroFacialAlertaProvider : IAlertaOrigemProvider
{
    private const string EntidadeOrigemTipo = "CadastroFacial";
    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.CadastroFacial;

    public CadastroFacialAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var fracos = await new ListarCadastrosFaciaisFracosQueryHandler(_db).Handle(new ListarCadastrosFaciaisFracosQuery(), ct);

        var itens = fracos.Select(f => new AlertaOrigemItem
        {
            EntidadeOrigemTipo = EntidadeOrigemTipo,
            EntidadeOrigemId = f.TrabalhadorId,
            DataVencimento = f.UltimaFalhaEm.Date,
            TipoAlertaVencendo = TipoAlerta.CadastroFacialFraco,
            TipoAlertaVencido = TipoAlerta.CadastroFacialFraco,
            Titulo = $"Refazer cadastro facial de {f.Nome}: {f.Falhas} falhas de reconhecimento em 30 dias",
            Descricao = $"Último motivo: {f.UltimoMotivo}. Abra o cadastro do funcionário e use \"Refazer cadastro facial\".",
            TrabalhadorId = f.TrabalhadorId,
            ObraId = f.ObraId,
        }).ToList();

        // Quem saiu da lista (cadastro refeito, falhas pararam): o motor só encerra alerta de item que ainda
        // recebe, então devolve o que ficou em aberto como substituído.
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
            TipoAlertaVencendo = TipoAlerta.CadastroFacialFraco,
            Substituido = true,
        }));

        return itens;
    }
}
