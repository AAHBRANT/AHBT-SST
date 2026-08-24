using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public class Setor : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string Nome { get; set; } = string.Empty;

    public ICollection<Equipe> Equipes { get; set; } = new List<Equipe>();
}
