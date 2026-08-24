namespace AAHBRANT.SST.Application.Equipes;

public class EquipeDto
{
    public Guid Id { get; set; }
    public Guid SetorId { get; set; }
    public string SetorNome { get; set; } = string.Empty;
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public Guid? EncarregadoId { get; set; }
    public string? EncarregadoNome { get; set; }
    public int QuantidadeTrabalhadores { get; set; }
}
