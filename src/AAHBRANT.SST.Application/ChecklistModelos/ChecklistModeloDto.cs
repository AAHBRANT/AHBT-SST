using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.ChecklistModelos;

public class ChecklistModeloDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public TipoInspecao TipoInspecao { get; set; }
    public int Versao { get; set; }
    public Guid? ChecklistModeloAnteriorId { get; set; }
    public int QuantidadeItens { get; set; }
}

public class ChecklistModeloItemDto
{
    public Guid Id { get; set; }
    public Guid ChecklistModeloId { get; set; }
    public int Ordem { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public bool ExigeFotografia { get; set; }
    public bool ExigeResponsavel { get; set; }
    public bool ExigePrazo { get; set; }
}

// Composição por query, não por tabela nova — mesmo princípio já usado em PermissaoTrabalhoDetalheDto.
public class ChecklistModeloDetalheDto
{
    public ChecklistModeloDto ChecklistModelo { get; set; } = null!;
    public List<ChecklistModeloItemDto> Itens { get; set; } = new();
}
