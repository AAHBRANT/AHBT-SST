using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Aptidoes;

public class AptidaoDto
{
    public Guid Id { get; set; }
    public Guid TrabalhadorId { get; set; }
    public string AtividadeCritica { get; set; } = string.Empty;
    public ResultadoAso Aptidao { get; set; }
    public DateTime DataAvaliacao { get; set; }
    public DateTime? DataValidade { get; set; }
    public string? MedicoResponsavel { get; set; }
    public string? Observacoes { get; set; }
}
