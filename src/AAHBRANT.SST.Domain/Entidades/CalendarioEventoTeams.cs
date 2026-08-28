using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Integração do Motor de Alertas com o Calendário do Teams (docs/superpowers/specs/
// 2026-08-28-calendario-teams-design.md) — rastreia o GraphEventId por origem para permitir
// atualizar/cancelar depois o mesmo evento. Uma linha por (EntidadeOrigemTipo, EntidadeOrigemId);
// hoje só "Alerta" é usado como origem, mas o campo é string pelo mesmo motivo de
// Alerta.EntidadeOrigemTipo — não exige migração de schema para novas origens.
public class CalendarioEventoTeams : AuditableEntity
{
    public string EntidadeOrigemTipo { get; set; } = string.Empty;
    public Guid EntidadeOrigemId { get; set; }

    public Guid OrganizadorUsuarioId { get; set; }
    public Usuario? OrganizadorUsuario { get; set; }

    public string? GraphEventId { get; set; }
    public StatusCalendarioEvento Status { get; set; } = StatusCalendarioEvento.Pendente;
    public string? MensagemErro { get; set; }
}
