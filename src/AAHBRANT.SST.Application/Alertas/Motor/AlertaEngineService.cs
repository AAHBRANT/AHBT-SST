using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// Implementação real do Motor Central de Alertas (requisito do usuário, 2026-08-24) — agrega todos
// os IAlertaOrigemProvider registrados via DI (Strategy pattern, mesmo espírito de
// EligibilityService) e compara cada item contra as RegraAlerta configuráveis do módulo dele.
// Chamado periodicamente pelo AlertaEngineWorker (AAHBRANT.SST.Worker).
public class AlertaEngineService : IAlertaEngineService
{
    private static readonly StatusAlerta[] StatusEmAberto =
        { StatusAlerta.Aberto, StatusAlerta.EmTratamento, StatusAlerta.Escalonado };

    // CalendarioEventoTeams.EntidadeOrigemTipo referencia o Alerta em si (não a origem mais profunda
    // dele, como AsoPeriodico/EPI) — ver docs/superpowers/specs/2026-08-28-calendario-teams-design.md
    // §4.1. Mesmo padrão em CriarAlertaCommand/AtualizarAlertaCommand/Resolver/Ignorar/ExcluirAlertaCommand.
    internal const string OrigemCalendarioAlerta = "Alerta";

    private readonly IAppDbContext _db;
    private readonly IEnumerable<IAlertaOrigemProvider> _providers;
    private readonly IFilaNotificacaoTeams _filaNotificacaoTeams;
    private readonly IFilaCalendarioTeams _filaCalendarioTeams;

    public AlertaEngineService(
        IAppDbContext db,
        IEnumerable<IAlertaOrigemProvider> providers,
        IFilaNotificacaoTeams filaNotificacaoTeams,
        IFilaCalendarioTeams filaCalendarioTeams)
    {
        _db = db;
        _providers = providers;
        _filaNotificacaoTeams = filaNotificacaoTeams;
        _filaCalendarioTeams = filaCalendarioTeams;
    }

    public async Task ProcessarAsync(CancellationToken ct = default)
    {
        var regrasPorModulo = (await _db.RegrasAlerta.ToListAsync(ct))
            .GroupBy(r => r.Modulo)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.DiasAntecedencia).ToList());

        var alertasEmAberto = await _db.Alertas
            .Where(a => StatusEmAberto.Contains(a.Status))
            .ToDictionaryAsync(a => (a.EntidadeOrigemTipo, a.EntidadeOrigemId), ct);

        // Alertas criados nesta execução com destinatário definido — enfileirados para envio
        // proativo no Teams somente depois do SaveChangesAsync (precisa do Id gerado pelo banco).
        // O destinatário vem de RegraAlerta.ResponsavelUsuarioId (responsável fixo por módulo,
        // requisito do usuário, 2026-08-25); fica vazio quando o módulo não tem responsável
        // configurado em Configurações.
        var alertasCriadosComDestinatario = new List<Alerta>();

        // Espelha o canal de calendário do Teams (docs/superpowers/specs/2026-08-28-calendario-teams-
        // design.md §4.4) nos mesmos três casos do motor: alerta novo com destinatário (Criar), alerta
        // existente com destinatário que teve título/severidade atualizados (Atualizar) e alerta
        // resolvido automaticamente porque o item saiu do vencimento (Cancelar).
        var alertasCriadosParaCalendario = new List<(Alerta Alerta, DateTime DataVencimento)>();
        var alertasAtualizadosComDestinatario = new List<(Alerta Alerta, DateTime DataVencimento)>();
        var alertasResolvidosAutomaticamente = new List<Alerta>();

        var hoje = DateTime.UtcNow.Date;

        foreach (var provider in _providers)
        {
            var itens = await provider.ObterItensAsync(ct);
            regrasPorModulo.TryGetValue(provider.Modulo, out var regrasModulo);
            regrasModulo ??= new List<RegraAlerta>();

            foreach (var item in itens)
            {
                var diasRestantes = (item.DataVencimento.Date - hoje).Days;
                var vencido = diasRestantes < 0;

                alertasEmAberto.TryGetValue((item.EntidadeOrigemTipo, item.EntidadeOrigemId), out var alertaExistente);

                // Vencido sempre alerta em Critico, mesmo sem regra configurada para isso — os
                // demais casos dependem da regra mais urgente cujo limiar cobre os dias restantes.
                // Guarda a RegraAlerta inteira (não só a Severidade) porque ela também carrega o
                // ResponsavelUsuarioId (requisito do usuário, 2026-08-25) usado para notificar no
                // Teams; item vencido usa a regra mais urgente do módulo como fonte do responsável,
                // mesmo que os dias restantes não caiam dentro do limiar dela.
                var regraAplicada = vencido
                    ? regrasModulo.OrderBy(r => r.DiasAntecedencia).FirstOrDefault()
                    : regrasModulo
                        .Where(r => diasRestantes <= r.DiasAntecedencia)
                        .OrderBy(r => r.DiasAntecedencia)
                        .FirstOrDefault();

                SeveridadeAlerta? severidade = vencido
                    ? SeveridadeAlerta.Critico
                    : (SeveridadeAlerta?)regraAplicada?.Severidade;

                if (severidade is null)
                {
                    // Item ficou dentro do prazo (ex.: nova higienização registrada) — se havia um
                    // alerta em aberto gerado por este motor, encerra automaticamente.
                    if (alertaExistente is not null)
                    {
                        alertaExistente.Status = StatusAlerta.Resolvido;
                        if (alertaExistente.DestinatarioUsuarioId.HasValue)
                            alertasResolvidosAutomaticamente.Add(alertaExistente);
                    }
                    continue;
                }

                var tipoAlerta = vencido ? (item.TipoAlertaVencido ?? item.TipoAlertaVencendo) : item.TipoAlertaVencendo;
                var titulo = vencido ? $"VENCIDO: {item.Titulo}" : $"{item.Titulo} (faltam {diasRestantes} dia(s))";

                if (alertaExistente is null)
                {
                    var novoAlerta = new Alerta
                    {
                        Tipo = tipoAlerta,
                        Severidade = severidade.Value,
                        Titulo = titulo,
                        Descricao = item.Descricao,
                        EntidadeOrigemTipo = item.EntidadeOrigemTipo,
                        EntidadeOrigemId = item.EntidadeOrigemId,
                        TrabalhadorId = item.TrabalhadorId,
                        ObraId = item.ObraId,
                        DestinatarioUsuarioId = regraAplicada?.ResponsavelUsuarioId,
                    };
                    _db.Alertas.Add(novoAlerta);

                    if (novoAlerta.DestinatarioUsuarioId.HasValue)
                    {
                        alertasCriadosComDestinatario.Add(novoAlerta);
                        alertasCriadosParaCalendario.Add((novoAlerta, item.DataVencimento));
                    }
                }
                else
                {
                    alertaExistente.Tipo = tipoAlerta;
                    alertaExistente.Severidade = severidade.Value;
                    alertaExistente.Titulo = titulo;

                    if (alertaExistente.DestinatarioUsuarioId.HasValue)
                        alertasAtualizadosComDestinatario.Add((alertaExistente, item.DataVencimento));
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        // Envio proativo no Teams — enfileira e segue em frente (PROJECT RULES.md §4); o envio de
        // fato e o retry em caso de falha acontecem em background (ver IFilaNotificacaoTeams).
        foreach (var alerta in alertasCriadosComDestinatario)
        {
            await _filaNotificacaoTeams.EnfileirarAsync(
                new NotificacaoTeamsMensagem(alerta.Id, alerta.DestinatarioUsuarioId!.Value, alerta.Titulo, alerta.Descricao),
                ct);
        }

        // Canal de calendário do Teams — mesmo princípio de "enfileira e segue em frente"; a criação/
        // atualização/cancelamento de fato no Graph acontece em background (ver IFilaCalendarioTeams).
        foreach (var (alerta, dataVencimento) in alertasCriadosParaCalendario)
        {
            await _filaCalendarioTeams.EnfileirarAsync(
                new CalendarioTeamsMensagem(
                    OrigemCalendarioAlerta, alerta.Id, OperacaoCalendarioTeams.Criar,
                    alerta.DestinatarioUsuarioId!.Value, alerta.Titulo, alerta.Descricao, dataVencimento),
                ct);
        }

        foreach (var (alerta, dataVencimento) in alertasAtualizadosComDestinatario)
        {
            await _filaCalendarioTeams.EnfileirarAsync(
                new CalendarioTeamsMensagem(
                    OrigemCalendarioAlerta, alerta.Id, OperacaoCalendarioTeams.Atualizar,
                    alerta.DestinatarioUsuarioId!.Value, alerta.Titulo, alerta.Descricao, dataVencimento),
                ct);
        }

        foreach (var alerta in alertasResolvidosAutomaticamente)
        {
            await _filaCalendarioTeams.EnfileirarAsync(
                new CalendarioTeamsMensagem(
                    OrigemCalendarioAlerta, alerta.Id, OperacaoCalendarioTeams.Cancelar,
                    alerta.DestinatarioUsuarioId!.Value, null, null, null),
                ct);
        }
    }
}
