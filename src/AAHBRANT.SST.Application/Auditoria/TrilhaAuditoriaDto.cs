namespace AAHBRANT.SST.Application.Auditoria;

public class TrilhaAuditoriaDto
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? UsuarioId { get; set; }
    public string? UsuarioNome { get; set; }
    public string Acao { get; set; } = string.Empty;
    public string EntidadeTipo { get; set; } = string.Empty;
    public Guid EntidadeId { get; set; }
    public string? DadosAntesJson { get; set; }
    public string? DadosDepoisJson { get; set; }
}
