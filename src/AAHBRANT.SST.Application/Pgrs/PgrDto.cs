using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Pgrs;

public class PgrDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public DateTime DataElaboracao { get; set; }
    public DateTime? DataProximaRevisao { get; set; }
    public DateTime? DataTermino { get; set; }
    public Guid? ResponsavelUsuarioId { get; set; }
    public StatusPgr Status { get; set; }
}

// "Caracterização das atividades" / "inventário de riscos" / "classificação dos riscos" (§16) —
// NÃO são tabelas novas, são a consulta de Atividade/Risco já cadastrados para a Obra do PGR
// (mesmo princípio de agregação por query descrito no plano da Fase D para InventarioRisco).
public class PgrDetalheDto
{
    public PgrDto Pgr { get; set; } = null!;
    public List<AtividadeCaracterizadaDto> Atividades { get; set; } = new();
    public List<PlanoAcaoItemDto> PlanoDeAcao { get; set; } = new();
    public List<PgrRevisaoDto> Revisoes { get; set; } = new();
}

public class AtividadeCaracterizadaDto
{
    public Guid AtividadeId { get; set; }
    public string AtividadeNome { get; set; } = string.Empty;
    public List<RiscoClassificadoDto> Riscos { get; set; } = new();
}

public class RiscoClassificadoDto
{
    public Guid RiscoId { get; set; }
    public string PerigoNome { get; set; } = string.Empty;
    public NivelRisco NivelRisco { get; set; }
    public string? ControlesExistentes { get; set; }
    public string? ControlesAdicionais { get; set; }
    public StatusControleRisco Status { get; set; }
}

public class PlanoAcaoItemDto
{
    public Guid Id { get; set; }
    public Guid PgrId { get; set; }
    public Guid? RiscoId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public Guid? ResponsavelUsuarioId { get; set; }
    public DateTime? Prazo { get; set; }
    public DateTime? DataConclusao { get; set; }
    public StatusControleRisco Status { get; set; }
}

public class PgrRevisaoDto
{
    public Guid Id { get; set; }
    public Guid PgrId { get; set; }
    public int NumeroRevisao { get; set; }
    public DateTime DataRevisao { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public Guid? ResponsavelUsuarioId { get; set; }
}
