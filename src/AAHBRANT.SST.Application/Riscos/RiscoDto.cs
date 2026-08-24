using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Riscos;

public class RiscoDto
{
    public Guid Id { get; set; }
    public Guid AtividadeId { get; set; }
    public Guid PerigoId { get; set; }
    public string? Ambiente { get; set; }
    public string? Exposicao { get; set; }
    public string? Consequencia { get; set; }
    public int Probabilidade { get; set; }
    public int Severidade { get; set; }
    public NivelRisco NivelRisco { get; set; }
    public string? ControlesExistentes { get; set; }
    public string? ControlesAdicionais { get; set; }
    public Guid? ResponsavelUsuarioId { get; set; }
    public DateTime? Prazo { get; set; }
    public StatusControleRisco Status { get; set; }
    public List<Guid> TrabalhadoresExpostosIds { get; set; } = new();
}
