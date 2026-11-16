using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

public class Funcao : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? CboCodigo { get; set; }
    public string? Descricao { get; set; }

    // Cor do capacete esperada para quem exerce a função (padrão AAHBRANT). SemPadrao = sem cor definida.
    public CorCapacete CorCapacete { get; set; } = CorCapacete.SemPadrao;

    public ICollection<Trabalhador> Trabalhadores { get; set; } = new List<Trabalhador>();
}
