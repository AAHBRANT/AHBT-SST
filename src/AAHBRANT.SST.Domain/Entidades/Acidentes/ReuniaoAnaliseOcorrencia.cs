using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Reunião de análise de acidente no Teams (09/10/2026, pedido do usuário: "o plano de ação já marca
// reunião sobre o assunto?"). Obrigatória em todo Acidente e Doença ocupacional registrado por
// relato. É também uma ação do plano (AcaoPlanoId), concluída pelo técnico depois da reunião.
public class ReuniaoAnaliseOcorrencia : AuditableEntity
{
    public Guid AcidenteId { get; set; }
    public Acidente? Acidente { get; set; }

    public Guid? AcaoPlanoId { get; set; }

    // Horário de Brasília.
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }

    public Guid? OrganizadorUsuarioId { get; set; }

    // "Nome · papel" de cada convidado, uma linha por pessoa — só para exibição; o convite em si
    // vai por e-mail pelo Teams.
    public string Participantes { get; set; } = string.Empty;

    public SituacaoReuniaoTeams Situacao { get; set; } = SituacaoReuniaoTeams.Pendente;
    public string? GraphEventId { get; set; }
    public string? LinkTeams { get; set; }

    // Motivo de a reunião não ter sido criada no Teams (Graph fora do ar, organizador sem conta
    // Microsoft, integração não configurada) — mostrado ao técnico para ele marcar por fora.
    public string? MotivoFalha { get; set; }
}
