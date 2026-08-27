using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public class TemplateBiometricoFutronic : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public string TemplateCriptografado { get; set; } = string.Empty;
    public DateTime CapturadoEm { get; set; }
}
