using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Aprs;

public class AprDto
{
    public Guid Id { get; set; }
    public Guid AtividadeId { get; set; }
    public string AtividadeNome { get; set; } = string.Empty;
    public string Local { get; set; } = string.Empty;
    public Guid? EquipeId { get; set; }
    public string? EquipeNome { get; set; }
    public DateTime Data { get; set; }
    public DateTime? Validade { get; set; }
    public StatusApr Status { get; set; }
    public Guid? AprovadoPorUsuarioId { get; set; }
    public string? AprovadoPorUsuarioNome { get; set; }
    public DateTime? DataAprovacao { get; set; }
    public string? MotivoReprovacao { get; set; }
}

public class AprEtapaDto
{
    public Guid Id { get; set; }
    public Guid AprId { get; set; }
    public int Ordem { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? MedidasPreventivas { get; set; }
    public List<Guid> RiscosIds { get; set; } = new();
}

public class AprResponsavelDto
{
    public Guid Id { get; set; }
    public Guid AprId { get; set; }
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
}

public class AprAssinaturaDto
{
    public Guid Id { get; set; }
    public Guid AprId { get; set; }
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
    public PapelAssinaturaApr Papel { get; set; }
    public DateTime DataAssinatura { get; set; }
}

// Composição por query, não por tabela nova — mesmo princípio já usado em PgrDetalheDto:
// une Apr + Etapas (com riscos ligados) + Responsáveis + Assinaturas.
public class AprDetalheDto
{
    public AprDto Apr { get; set; } = null!;
    public List<AprEtapaDto> Etapas { get; set; } = new();
    public List<AprResponsavelDto> Responsaveis { get; set; } = new();
    public List<AprAssinaturaDto> Assinaturas { get; set; } = new();
}
