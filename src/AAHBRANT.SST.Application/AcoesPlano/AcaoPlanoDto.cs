using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.AcoesPlano;

public class AcaoPlanoDto
{
    public Guid Id { get; set; }
    public string OrigemTipo { get; set; } = string.Empty;
    public Guid OrigemId { get; set; }
    public TipoAcaoPlano Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelUsuarioNome { get; set; }
    public PrioridadeAcao Prioridade { get; set; }
    public DateTime? Prazo { get; set; }
    public StatusControleRisco Status { get; set; }
    public DateTime? DataConclusao { get; set; }
    public DateTime? DataValidacao { get; set; }
    public Guid? ValidadoPorUsuarioId { get; set; }
    public string? ValidadoPorUsuarioNome { get; set; }
}
