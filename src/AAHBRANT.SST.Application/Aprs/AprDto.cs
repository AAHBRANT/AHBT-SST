using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Aprs;

public class AprDto
{
    public Guid Id { get; set; }
    public string? NumeroApr { get; set; }
    public Guid AtividadeId { get; set; }
    public string AtividadeNome { get; set; } = string.Empty;
    public string? ObraNome { get; set; }
    public string Local { get; set; } = string.Empty;
    public string? MaquinasEquipamentos { get; set; }
    public string? PgrReferencia { get; set; }
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

public class AprEtapaRiscoDto
{
    public Guid Id { get; set; }
    public Guid AprEtapaId { get; set; }
    public string PerigoEventoPerigoso { get; set; } = string.Empty;
    public string? FonteCircunstancia { get; set; }
    public string? PossiveisLesoes { get; set; }
    public string? TrabalhadoresExpostos { get; set; }
    public int ProbabilidadeInicial { get; set; }
    public int SeveridadeInicial { get; set; }
    public NivelRiscoApr NivelRiscoInicial { get; set; }
    public string? MedidasPrevencao { get; set; }
    public string? Responsavel { get; set; }
    public int ProbabilidadeResidual { get; set; }
    public int SeveridadeResidual { get; set; }
    public NivelRiscoApr NivelRiscoResidual { get; set; }
}

public class AprEtapaDto
{
    public Guid Id { get; set; }
    public Guid AprId { get; set; }
    public int Ordem { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public List<AprEtapaRiscoDto> Riscos { get; set; } = new();
}

public class AprResponsavelDto
{
    public Guid Id { get; set; }
    public Guid AprId { get; set; }
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
    public string? TrabalhadorFuncaoNome { get; set; }
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
// une Apr + Etapas (com riscos completos) + Responsáveis + Assinaturas.
public class AprDetalheDto
{
    public AprDto Apr { get; set; } = null!;
    public List<AprEtapaDto> Etapas { get; set; } = new();
    public List<AprResponsavelDto> Responsaveis { get; set; } = new();
    public List<AprAssinaturaDto> Assinaturas { get; set; } = new();
}
