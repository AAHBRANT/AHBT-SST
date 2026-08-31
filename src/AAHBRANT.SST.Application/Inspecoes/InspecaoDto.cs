using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Inspecoes;

public class InspecaoDto
{
    public Guid Id { get; set; }
    public TipoInspecao TipoInspecao { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public Guid? AtividadeId { get; set; }
    public string? AtividadeNome { get; set; }
    public Guid ChecklistModeloId { get; set; }
    public string ChecklistModeloNome { get; set; } = string.Empty;
    public int ChecklistModeloVersao { get; set; }
    public DateTime Data { get; set; }
    public Guid ResponsavelUsuarioId { get; set; }
    public string ResponsavelUsuarioNome { get; set; } = string.Empty;
    public StatusInspecao Status { get; set; }
    public int TotalItens { get; set; }
    public int ItensRespondidos { get; set; }
    public int ItensNaoConformes { get; set; }
}

public class InspecaoItemRespostaDto
{
    public Guid Id { get; set; }
    public Guid InspecaoId { get; set; }
    public Guid ChecklistModeloItemId { get; set; }
    public int Ordem { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public bool ExigeFotografia { get; set; }
    public bool ExigeResponsavel { get; set; }
    public bool ExigePrazo { get; set; }
    public StatusItemChecklist? StatusItem { get; set; }
    public string? Observacao { get; set; }
    // Achado tipo "Patrulha de Segurança" (planilha do usuário, 31/08) — ver disclosure em
    // InspecaoItemResposta.cs sobre reaproveitar o checklist com descrição editável por execução.
    public string? Local { get; set; }
    public string? PlanoDeAcao { get; set; }
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelUsuarioNome { get; set; }
    public DateTime? Prazo { get; set; }
    public bool TemFoto { get; set; }
    public bool TemFotoDepois { get; set; }
    public Guid? NaoConformidadeId { get; set; }
}

// Composição por query, não por tabela nova — mesmo princípio já usado em PermissaoTrabalhoDetalheDto.
public class InspecaoDetalheDto
{
    public InspecaoDto Inspecao { get; set; } = null!;
    public List<InspecaoItemRespostaDto> Respostas { get; set; } = new();
}
