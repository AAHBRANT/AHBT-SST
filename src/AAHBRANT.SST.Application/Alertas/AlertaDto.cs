using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Alertas;

public class AlertaDto
{
    public Guid Id { get; set; }
    public TipoAlerta Tipo { get; set; }
    public SeveridadeAlerta Severidade { get; set; }
    public StatusAlerta Status { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    public string EntidadeOrigemTipo { get; set; } = string.Empty;
    public Guid EntidadeOrigemId { get; set; }

    public Guid? TrabalhadorId { get; set; }
    public string? TrabalhadorNome { get; set; }

    public Guid? ObraId { get; set; }
    public string? ObraNome { get; set; }

    public Guid? DestinatarioUsuarioId { get; set; }
    public string? DestinatarioUsuarioNome { get; set; }

    public DateTime? DataLimiteTratamento { get; set; }
    public Guid? EscalonadoParaUsuarioId { get; set; }
    public string? EscalonadoParaUsuarioNome { get; set; }
    public DateTime? DataEscalonamento { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
