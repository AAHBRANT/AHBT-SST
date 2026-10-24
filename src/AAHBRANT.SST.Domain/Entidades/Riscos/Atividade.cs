using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Elo entre Obra e Perigo/Risco na cadeia do §46 da Base de Conhecimento
// (Cadastro obra → Atividades → Perigos → Avaliação de riscos → ...).
public class Atividade : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    // GHE do PGR a que a atividade pertence (nulo nas atividades cadastradas à mão, sem GHE).
    public Guid? GheId { get; set; }
    public Ghe? Ghe { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    public ICollection<Risco> Riscos { get; set; } = new List<Risco>();

    // Detalhes para a Ordem de Serviço (20/10/2026). Condições nulas = usa o ambiente/jornada do GHE.
    public string? CondicoesExecucao { get; set; }
    // Uma ferramenta/equipamento por linha.
    public string? Ferramentas { get; set; }
    public ICollection<AtividadeEpc> Epcs { get; set; } = new List<AtividadeEpc>();
    public ICollection<AtividadeDocumento> Documentos { get; set; } = new List<AtividadeDocumento>();
    public ICollection<AtividadeItemOs> ItensOs { get; set; } = new List<AtividadeItemOs>();
}

public class AtividadeEpc : AuditableEntity
{
    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }
    public Guid CatalogoEpcId { get; set; }
    public CatalogoEpc? CatalogoEpc { get; set; }
}

// Procedimento ou IT (MaterialApoio do tipo controlado) aplicável à atividade.
public class AtividadeDocumento : AuditableEntity
{
    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }
    public Guid MaterialApoioId { get; set; }
    public MaterialApoio? MaterialApoio { get; set; }
}

// "Interromper e avisar a liderança quando…", "não executar sem…" e autorizações exigidas. Mesma regra
// das medidas: escrito pela SST entra validado; sugerido pela IA fica pendente até a SST decidir.
public class AtividadeItemOs : AuditableEntity
{
    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }
    public TipoItemAtividadeOs Tipo { get; set; }
    public string Texto { get; set; } = string.Empty;
    // Só em Restricao: o que precisa para poder executar (capacitação, autorização, liberação).
    public string? Requisito { get; set; }
    public OrigemMedidaPrevencao OrigemItem { get; set; }
    public StatusMedidaPrevencao Status { get; set; }
    public Guid? ValidadoPorUsuarioId { get; set; }
    public string? ValidadoPorNome { get; set; }
    public DateTime? ValidadoEmUtc { get; set; }
}
