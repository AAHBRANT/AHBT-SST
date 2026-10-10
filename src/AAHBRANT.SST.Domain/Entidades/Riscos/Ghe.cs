using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Grupo Homogêneo de Exposição do PGR (NR-01, item 17 "GRUPOS HOMOGÊNEOS DE EXPOSIÇÃO – GHE" dos
// PGRs da AAHBRANT): conjunto de funções da obra expostas aos mesmos riscos. É por obra — o mesmo
// "GHE 01" de duas obras tem funções e riscos diferentes. Os riscos do GHE continuam na cadeia já
// existente Atividade → Risco (Atividade.GheId), em vez de uma tabela de risco paralela, para que
// APR/PT/inventário/alertas que já leem Risco enxerguem o inventário importado sem mudança.
public class Ghe : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    // Número do GHE no documento (GHE 01 → 1). Único por obra.
    public int Numero { get; set; }
    public string? Setor { get; set; }
    public string? JornadaTrabalho { get; set; }
    public string? DescricaoAmbiente { get; set; }
    public string? AtividadesCriticas { get; set; }
    public string? FonteGeradora { get; set; }
    public string? MedidasProtecaoExistentes { get; set; }

    public ICollection<GheFuncao> Funcoes { get; set; } = new List<GheFuncao>();
    public ICollection<Atividade> Atividades { get; set; } = new List<Atividade>();
}

// Função exposta no GHE. Funcao é global (o G-RH cria pelo nome do cargo); o vínculo com o GHE é o
// que é por obra.
public class GheFuncao : AuditableEntity
{
    public Guid GheId { get; set; }
    public Ghe? Ghe { get; set; }

    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }

    // "Quantidade Expostos" da tabela de GHE do PGR — retrato do documento, não a contagem real de
    // trabalhadores da obra.
    public int? QuantidadeExpostos { get; set; }
    public string? DescricaoAtividades { get; set; }
}
