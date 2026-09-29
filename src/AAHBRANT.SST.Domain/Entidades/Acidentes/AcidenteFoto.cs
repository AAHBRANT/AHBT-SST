using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public class AcidenteFoto : AuditableEntity
{
    public Guid AcidenteId { get; set; }
    public Acidente? Acidente { get; set; }
    public int Ordem { get; set; }
    public byte[] FotoConteudo { get; set; } = Array.Empty<byte>();
    public string FotoContentType { get; set; } = string.Empty;
    public string? FotoMetadadosJson { get; set; }
}
