using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Decisão não literal da Base de Conhecimento: integração solicitada em 29/09/2026.
// Contrato e regras em docs/integracoes/g-supri.md. O G-SUPRI é a origem do recebimento.
public class GsupriObra : AuditableEntity
{
    public string CodigoExterno { get; set; } = "";
    public Guid ObraId { get; set; }
}

public class GsupriProduto : AuditableEntity
{
    public string CodigoExterno { get; set; } = "";
    public string Unidade { get; set; } = "";
    public string Categoria { get; set; } = "";
    public Guid? CatalogoId { get; set; }
    public string Tamanho { get; set; } = "";
    public decimal FatorConversao { get; set; } = 1;
}

public class GsupriRecebimento : AuditableEntity
{
    public string RecebimentoExternoId { get; set; } = "";
    public string ObraCodigo { get; set; } = "";
    public Guid? ObraId { get; set; }
    public int Versao { get; set; }
    public string Hash { get; set; } = "";
    public string DadosJson { get; set; } = "";
    public string Status { get; set; } = "Pendente";
    public string? Pendencia { get; set; }
    public ICollection<GsupriRecebimentoItem> Itens { get; set; } = new List<GsupriRecebimentoItem>();
}

public class GsupriRecebimentoItem : AuditableEntity
{
    public Guid RecebimentoId { get; set; }
    public GsupriRecebimento Recebimento { get; set; } = null!;
    public string ItemExternoId { get; set; } = "";
    public string ProdutoCodigo { get; set; } = "";
    public string Unidade { get; set; } = "";
    public Guid? ProdutoVinculoId { get; set; }
    public GsupriProduto? ProdutoVinculo { get; set; }
    public int QuantidadeAplicada { get; set; }
    public string? Pendencia { get; set; }
}

// Cada evento conserva o payload recebido, inclusive versões anteriores e cancelamentos.
public class GsupriEvento : AuditableEntity
{
    public string EventoExternoId { get; set; } = "";
    public Guid RecebimentoId { get; set; }
    public string Hash { get; set; } = "";
    public string DadosJson { get; set; } = "";
}
