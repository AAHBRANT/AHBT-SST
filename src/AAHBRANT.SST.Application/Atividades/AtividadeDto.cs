namespace AAHBRANT.SST.Application.Atividades;

public class AtividadeDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    // Consulta da aba Atividades (07/11/2026): GHE de origem (PGR) e contagem de riscos por faixa.
    public int? GheNumero { get; set; }
    public string? GheSetor { get; set; }
    public int RiscosAltos { get; set; }
    public int RiscosModerados { get; set; }
    public int RiscosBaixos { get; set; }
}
