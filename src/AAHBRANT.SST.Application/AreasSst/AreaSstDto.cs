using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.AreasSst;

public class AreaSstDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public TipoArea Tipo { get; set; }
    public Guid ObraId { get; set; }
    public string? DetalhesLocalizacao { get; set; }
    public List<string> Riscos { get; set; } = new();
    public List<string> Requisitos { get; set; } = new();
    public StatusArea Status { get; set; }
}
