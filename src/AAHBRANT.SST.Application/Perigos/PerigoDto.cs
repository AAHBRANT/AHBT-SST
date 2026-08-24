namespace AAHBRANT.SST.Application.Perigos;

public class PerigoDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Agente { get; set; }
    public string? Fonte { get; set; }
    public string? Descricao { get; set; }
}
