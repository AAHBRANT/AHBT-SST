using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

public class Alerta : AuditableEntity
{
    public TipoAlerta Tipo { get; set; }
    public SeveridadeAlerta Severidade { get; set; } = SeveridadeAlerta.Atencao;
    public StatusAlerta Status { get; set; } = StatusAlerta.Aberto;

    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    // Referência polimórfica à entidade de origem (Aso, Treinamento, EntregaEpi etc.)
    public string EntidadeOrigemTipo { get; set; } = string.Empty;
    public Guid EntidadeOrigemId { get; set; }

    public Guid? TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public Guid? ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid? DestinatarioUsuarioId { get; set; }
    public Usuario? DestinatarioUsuario { get; set; }

    // Suporte a escalonamento automático (Análise de Oportunidades, Nível 2)
    public DateTime? DataLimiteTratamento { get; set; }
    public Guid? EscalonadoParaUsuarioId { get; set; }
    public Usuario? EscalonadoParaUsuario { get; set; }
    public DateTime? DataEscalonamento { get; set; }

    public ICollection<AlertaHistoricoEnvio> HistoricoEnvios { get; set; } = new List<AlertaHistoricoEnvio>();
}

// Alimenta a fila de retry no Azure Service Bus (PROJECT RULES.md §4)
public class AlertaHistoricoEnvio : AuditableEntity
{
    public Guid AlertaId { get; set; }
    public Alerta? Alerta { get; set; }

    public string Canal { get; set; } = string.Empty; // Bot | ActivityFeed | Email | Calendario
    public bool Sucesso { get; set; }
    public int NumeroTentativa { get; set; } = 1;
    public string? MensagemErro { get; set; }
}
