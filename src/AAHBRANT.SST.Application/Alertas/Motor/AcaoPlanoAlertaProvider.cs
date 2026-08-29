using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// Procedimento de Inspeção Técnica de Campo (§8) — "ação corretiva com prazo vencido deve gerar
// alerta". AcaoPlano é a entidade polimórfica genérica (OrigemTipo/OrigemId) reaproveitada hoje por
// NaoConformidade e Acidente (ver disclosure em AcaoPlano.cs) — este provider cobre as duas origens
// numa tacada só, em vez de um provider por origem, já que o campo relevante (Prazo) é o mesmo.
// TipoModuloAlerta.PlanoAcao e TipoAlerta.AcaoAtrasada já estavam reservados no enum para isto.
//
// Limitação conhecida e avisada ao usuário (mesma do restante do Motor Central de Alertas): o
// destinatário do alerta vem de RegraAlerta.ResponsavelUsuarioId, um responsável FIXO por módulo
// (todo o sistema, não por obra/contrato) — não o ResponsavelUsuarioId da própria AcaoPlano. O
// escalonamento "ao gestor da obra/contrato" (§8) não é reproduzido por esta fatia; a origem em si
// (NaoConformidade.ResponsavelUsuarioId / Acidente) segue notificada de forma imediata pelos
// próprios comandos de fluxo (ex. EnviarNaoConformidadeCommand, DevolverNaoConformidadeCommand).
public class AcaoPlanoAlertaProvider : IAlertaOrigemProvider
{
    private readonly IAppDbContext _db;

    public TipoModuloAlerta Modulo => TipoModuloAlerta.PlanoAcao;

    public AcaoPlanoAlertaProvider(IAppDbContext db) => _db = db;

    public async Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default)
    {
        var acoes = await _db.AcoesPlano
            .Where(a => a.Status != StatusControleRisco.Concluido && a.Prazo != null)
            .ToListAsync(ct);

        if (acoes.Count == 0) return new List<AlertaOrigemItem>();

        var idsNaoConformidade = acoes
            .Where(a => a.OrigemTipo == nameof(NaoConformidade))
            .Select(a => a.OrigemId)
            .ToList();
        var obrasPorNc = await _db.NaoConformidades
            .Include(n => n.Atividade)
            .Where(n => idsNaoConformidade.Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, n => n.Atividade?.ObraId, ct);

        var idsAcidente = acoes
            .Where(a => a.OrigemTipo == nameof(Domain.Entidades.Acidente))
            .Select(a => a.OrigemId)
            .ToList();
        var obrasPorAcidente = await _db.Acidentes
            .Where(a => idsAcidente.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => (Guid?)a.ObraId, ct);

        return acoes.Select(acao =>
        {
            Guid? obraId = acao.OrigemTipo switch
            {
                nameof(NaoConformidade) => obrasPorNc.GetValueOrDefault(acao.OrigemId),
                nameof(Domain.Entidades.Acidente) => obrasPorAcidente.GetValueOrDefault(acao.OrigemId),
                _ => null,
            };

            return new AlertaOrigemItem
            {
                EntidadeOrigemTipo = nameof(AcaoPlano),
                EntidadeOrigemId = acao.Id,
                DataVencimento = acao.Prazo!.Value,
                TipoAlertaVencendo = TipoAlerta.AcaoAtrasada,
                Titulo = $"Ação de plano atrasada — {acao.Descricao}",
                ObraId = obraId,
            };
        }).ToList();
    }
}
