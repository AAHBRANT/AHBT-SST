using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.AreasSst;

// Formato do "card público" descrito no doc de Identificação (§3.B.4): dados de uma Área
// suficientes para orientar quem escaneou a NFC/QR em campo, sem expor Id interno/ObraId.
public class AreaPublicaDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public TipoArea Tipo { get; set; }
    public StatusArea Status { get; set; }
    public List<string> Riscos { get; set; } = new();
    public List<string> Requisitos { get; set; } = new();
    public string? DetalhesLocalizacao { get; set; }
}
