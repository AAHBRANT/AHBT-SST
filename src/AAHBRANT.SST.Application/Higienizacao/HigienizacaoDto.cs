namespace AAHBRANT.SST.Application.Higienizacao;

public class ItemHigienizacaoDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Local { get; set; }
    public int PeriodicidadeDias { get; set; }
    public DateTime? UltimaHigienizacaoEm { get; set; }
    public DateTime ProximoVencimentoEm { get; set; }
    public int TotalRegistros { get; set; }
}

public class RegistroHigienizacaoDto
{
    public Guid Id { get; set; }
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
    public DateTime DataHora { get; set; }
    public string? Observacoes { get; set; }
}

// Composição por query (mesmo padrão de DdsDetalheDto) — não é uma tabela nova.
public class ItemHigienizacaoDetalheDto
{
    public ItemHigienizacaoDto Item { get; set; } = null!;
    public List<RegistroHigienizacaoDto> Registros { get; set; } = new();
}
