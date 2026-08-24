using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Elo entre Obra e Perigo/Risco na cadeia do §46 da Base de Conhecimento
// (Cadastro obra → Atividades → Perigos → Avaliação de riscos → ...).
public class Atividade : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    public ICollection<Risco> Riscos { get; set; } = new List<Risco>();
}
