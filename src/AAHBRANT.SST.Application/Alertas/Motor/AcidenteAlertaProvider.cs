using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Acidentes.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// Ocorrências (acidentes) que pedem acompanhamento: (a) óbito com providências do checklist ainda em aberto e
// (b) ocorrência em "Aguardando laudo" por mais tempo que o limite abaixo. O Acidente respeita escopo por obra
// e soft delete pelo filtro global do DbContext; o alerta carrega a ObraId para o mesmo escopo no painel.
//
// Destinatário: a ocorrência não tem responsável próprio, então o item não define destinatário e vale o
// responsável FIXO do módulo (RegraAlerta.ResponsavelUsuarioId, configurado em Configurações), como nos demais
// providers sem responsável. Mesmo padrão do CadastroFacialAlertaProvider: o motor só encerra alerta de item
// que ainda recebe, então o que saiu da lista volta como "Substituido".
//
// Texto objetivo e institucional: não afirma prazo legal; para óbito remete ao jurídico.
public class AcidenteAlertaProvider : IAlertaOrigemProvider
{
    // Valor provisório; confirmar com o usuário.
    public const int DiasAlertaLaudoAguardado = 30;

    public const string OrigemObito = "AcidenteObito";
    public const string OrigemLaudo = "AcidenteLaudo";

    private static readonly StatusAlerta[] StatusEmAberto =
        { StatusAlerta.Aberto, StatusAlerta.EmTratamento, StatusAlerta.Escalonado };

    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.Acidente;

    public AcidenteAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var hoje = DateTime.UtcNow.Date;

        var idsObitoExterno = await _db.AcidentesPessoasExternas
            .Where(p => p.Resultado == ResultadoPessoaExterna.Obito)
            .Select(p => p.AcidenteId)
            .Distinct()
            .ToListAsync(ct);

        var candidatos = await _db.Acidentes
            .Where(a => a.Gravidade == GravidadeAcidente.Obito
                || idsObitoExterno.Contains(a.Id)
                || a.Status == StatusAcidente.AguardandoLaudo)
            .Select(a => new
            {
                a.Id, a.ObraId, a.Data, a.Gravidade, a.Status, a.CreatedAtUtc, a.AguardandoLaudoDesde, a.ProvidenciasObitoJson,
            })
            .ToListAsync(ct);

        var itens = new List<AlertaOrigemItem>();
        var obitoEmitidos = new HashSet<Guid>();
        var laudoEmitidos = new HashSet<Guid>();

        foreach (var a in candidatos)
        {
            // Só óbito de ocorrência em andamento e registrada a partir do corte (decisão provisória; confirmar com o usuário):
            // evita inundar com óbitos antigos no deploy. Concluída ou anterior ao corte não alerta; se já havia
            // alerta, ele volta como Substituido pelo laço final (o checklist de ocorrência concluída não pode mais ser alterado).
            var obito = (a.Gravidade == GravidadeAcidente.Obito || idsObitoExterno.Contains(a.Id))
                && a.Status != StatusAcidente.Concluido
                && a.CreatedAtUtc >= AvancarStatusAcidenteCommandHandler.ExigeTresFotosParaTodosOsTiposApartirDe;
            if (obito)
            {
                var pendentes = ProvidenciasObitoAcidente.Ler(a.ProvidenciasObitoJson).Count(p => !p.Concluida);
                obitoEmitidos.Add(a.Id);
                itens.Add(new AlertaOrigemItem
                {
                    EntidadeOrigemTipo = OrigemObito,
                    EntidadeOrigemId = a.Id,
                    // Já nasce na data da ocorrência (no máximo ontem): sem regra configurada, o motor só avisa item com data passada.
                    DataVencimento = a.Data.Date < hoje ? a.Data.Date : hoje.AddDays(-1),
                    TipoAlertaVencendo = TipoAlerta.ObitoProvidenciasPendentes,
                    TipoAlertaVencido = TipoAlerta.ObitoProvidenciasPendentes,
                    Titulo = $"Ocorrência com óbito: {pendentes} providência(s) do checklist em aberto",
                    Descricao = "Abra a ocorrência e acompanhe o checklist de providências do óbito. Confirmar providências com o jurídico.",
                    ObraId = a.ObraId,
                    Substituido = pendentes == 0,
                });
            }

            if (a.Status == StatusAcidente.AguardandoLaudo && a.AguardandoLaudoDesde.HasValue)
            {
                laudoEmitidos.Add(a.Id);
                var dias = (hoje - a.AguardandoLaudoDesde.Value.Date).Days;
                itens.Add(new AlertaOrigemItem
                {
                    EntidadeOrigemTipo = OrigemLaudo,
                    EntidadeOrigemId = a.Id,
                    // Vence quando completa o limite; só avisa depois dele.
                    DataVencimento = a.AguardandoLaudoDesde.Value.Date.AddDays(DiasAlertaLaudoAguardado),
                    TipoAlertaVencendo = TipoAlerta.OcorrenciaAguardandoLaudo,
                    TipoAlertaVencido = TipoAlerta.OcorrenciaAguardandoLaudo,
                    Titulo = $"Ocorrência aguardando laudo há {dias} dias",
                    Descricao = "Confirme com a área responsável o andamento do laudo e, quando ele chegar, anexe-o e retome a investigação.",
                    ObraId = a.ObraId,
                });
            }
        }

        // Alertas em aberto de ocorrência que saiu do critério (laudo chegou, ocorrência excluída, etc.).
        var emAberto = await _db.Alertas.AsNoTracking()
            .Where(al => (al.EntidadeOrigemTipo == OrigemObito || al.EntidadeOrigemTipo == OrigemLaudo)
                && StatusEmAberto.Contains(al.Status))
            .Select(al => new { al.EntidadeOrigemTipo, al.EntidadeOrigemId })
            .ToListAsync(ct);

        foreach (var al in emAberto.Distinct())
        {
            var emitidos = al.EntidadeOrigemTipo == OrigemObito ? obitoEmitidos : laudoEmitidos;
            if (emitidos.Contains(al.EntidadeOrigemId)) continue;
            itens.Add(new AlertaOrigemItem
            {
                EntidadeOrigemTipo = al.EntidadeOrigemTipo,
                EntidadeOrigemId = al.EntidadeOrigemId,
                DataVencimento = hoje,
                TipoAlertaVencendo = al.EntidadeOrigemTipo == OrigemObito
                    ? TipoAlerta.ObitoProvidenciasPendentes : TipoAlerta.OcorrenciaAguardandoLaudo,
                Substituido = true,
            });
        }

        return itens;
    }
}
