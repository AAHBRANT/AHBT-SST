using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Medida de prevenção de um risco, separada pela fase do trabalho (antes, durante, após) para a
// Ordem de Serviço (NR-01 1.4.1 "c"). O que veio do texto do PGR entra Validada (o PGR já foi aprovado
// pelo engenheiro); o que a IA acrescenta entra Pendente e só vale depois que a SST aceitar. Só medida
// Validada vai para a OS. O texto livre original (Risco.ControlesExistentes/ControlesAdicionais) é
// preservado.
public class MedidaPrevencao : AuditableEntity
{
    public Guid RiscoId { get; set; }
    public Risco? Risco { get; set; }

    public FaseMedidaPrevencao Fase { get; set; }
    public string Texto { get; set; } = string.Empty;
    public OrigemMedidaPrevencao OrigemMedida { get; set; }
    public StatusMedidaPrevencao Status { get; set; }
    public int Ordem { get; set; }

    public Guid? ValidadoPorUsuarioId { get; set; }
    public string? ValidadoPorNome { get; set; }
    public DateTime? ValidadoEmUtc { get; set; }
}
