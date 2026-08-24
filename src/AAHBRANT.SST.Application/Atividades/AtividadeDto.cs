namespace AAHBRANT.SST.Application.Atividades;

public class AtividadeDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
}
