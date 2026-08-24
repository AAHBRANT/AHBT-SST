using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Catálogo reutilizável de perigos (§14/§15 da Base de Conhecimento) — distinto do
// registro de avaliação (Risco), pode ser referenciado por várias atividades/riscos.
public class Perigo : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Agente { get; set; }
    public string? Fonte { get; set; }
    public string? Descricao { get; set; }

    public ICollection<Risco> Riscos { get; set; } = new List<Risco>();
}
