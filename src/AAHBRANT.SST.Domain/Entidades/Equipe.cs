using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public class Equipe : AuditableEntity
{
    public Guid SetorId { get; set; }
    public Setor? Setor { get; set; }

    public string Nome { get; set; } = string.Empty;
    public Guid? EncarregadoId { get; set; }
    public Trabalhador? Encarregado { get; set; }

    public ICollection<Trabalhador> Trabalhadores { get; set; } = new List<Trabalhador>();
}
