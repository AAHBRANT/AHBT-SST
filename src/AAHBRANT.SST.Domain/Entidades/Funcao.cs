using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public class Funcao : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? CboCodigo { get; set; }
    public string? Descricao { get; set; }

    public ICollection<Trabalhador> Trabalhadores { get; set; } = new List<Trabalhador>();
}
