using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.ExamesComplementares;

public class ExameComplementarDto
{
    public Guid Id { get; set; }
    public Guid TrabalhadorId { get; set; }
    public Guid? AsoId { get; set; }
    public TipoExameComplementar Tipo { get; set; }
    public DateTime DataRealizacao { get; set; }
    public DateTime DataValidade { get; set; }
    public string Resultado { get; set; } = string.Empty;
    public string? Observacoes { get; set; }
    public string? ResponsavelTecnico { get; set; }
}
