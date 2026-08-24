using AAHBRANT.SST.Application.AcoesPlano;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.NaoConformidades;

public class NaoConformidadeDto
{
    public Guid Id { get; set; }
    public OrigemNaoConformidade OrigemDeteccao { get; set; }
    public string? RequisitoRelacionado { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Local { get; set; }
    public Guid? AtividadeId { get; set; }
    public string? AtividadeNome { get; set; }
    public Guid? RiscoId { get; set; }
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelUsuarioNome { get; set; }
    public DateTime? Prazo { get; set; }
    public StatusNaoConformidade Status { get; set; }
    public DateTime? DataConclusao { get; set; }
    public string? ObservacoesEncerramento { get; set; }
}

// Composição por query, não por tabela nova — mesmo princípio já usado em InspecaoDetalheDto /
// PermissaoTrabalhoDetalheDto. As ações corretiva/preventiva vinculadas são lidas de AcaoPlano
// filtrando por OrigemTipo=nameof(NaoConformidade) e OrigemId=Id.
public class NaoConformidadeDetalheDto
{
    public NaoConformidadeDto NaoConformidade { get; set; } = null!;
    public List<AcaoPlanoDto> AcoesPlano { get; set; } = new();
}
