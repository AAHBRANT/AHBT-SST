using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Ativos;

public class AtivoSstDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public TipoAtivo TipoAtivo { get; set; }
    public string Identificacao { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? Localizacao { get; set; }
    public DateTime DataValidade { get; set; }
    public string? Observacoes { get; set; }
}
