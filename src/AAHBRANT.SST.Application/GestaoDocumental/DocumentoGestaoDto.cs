using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.GestaoDocumental;

public class DocumentoGestaoDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public string? Categoria { get; set; }
    public string? OrigemDocumento { get; set; }
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelUsuarioNome { get; set; }
    public string? Versao { get; set; }
    public DateTime? Validade { get; set; }
    public DateTime DataEmissao { get; set; }
    public DateTime? DataRevisao { get; set; }
    public Guid? RequisitoLegalId { get; set; }
    public string? RequisitoLegalCodigo { get; set; }
    public Guid? ObraId { get; set; }
    public string? ObraNome { get; set; }
    public Guid? SetorId { get; set; }
    public string? SetorNome { get; set; }
    public StatusDocumentoGestao Status { get; set; }
    public string? Arquivo { get; set; }
}

public class DocumentoRevisaoDto
{
    public Guid Id { get; set; }
    public int NumeroRevisao { get; set; }
    public DateTime DataRevisao { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelUsuarioNome { get; set; }
}

// Composição por query (mesmo princípio já usado em RequisitoLegalDetalheDto/NaoConformidadeDetalheDto):
// o "histórico" (§31) é a lista de DocumentoRevisao vinculada, mais recente primeiro.
public class DocumentoGestaoDetalheDto
{
    public DocumentoGestaoDto Documento { get; set; } = null!;
    public List<DocumentoRevisaoDto> Historico { get; set; } = new();
}
