namespace AAHBRANT.SST.Domain.Common;

public enum OrigemRegistro
{
    Manual = 0,
    Importacao = 1,
    Ocr = 2,
    IntegracaoGraph = 3
}

public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedBy { get; set; }

    public OrigemRegistro Origem { get; set; } = OrigemRegistro.Manual;

    public bool Ativo { get; set; } = true;

    public byte[]? RowVersion { get; set; }
}
