using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Permissão de Trabalho (Base de Conhecimento §18): campos literais citados —
// atividade; local; equipe; data; horário; validade; perigos; controles; requisitos;
// responsáveis; autorização; encerramento. "Deverá existir rastreabilidade de todas as
// PTs emitidas" — coberta pelo padrão de auditoria (AuditableEntity) já usado em todo módulo,
// sem tabela própria (mesma decisão já tomada para Risco/Apr/Pgr).
// Trabalho em altura/espaço confinado/segurança elétrica (§19/§20/§21) são módulos
// especializados à parte, fora da lista literal do MVP (§47 lista só o item "13. PT") —
// não implementados nesta fatia.
public class PermissaoTrabalho : AuditableEntity
{
    public Guid AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }

    public string Local { get; set; } = string.Empty;

    public Guid? EquipeId { get; set; }
    public Equipe? Equipe { get; set; }

    public DateTime Data { get; set; }

    // "Horário" (§18) — documento não detalha se é um horário único ou um intervalo de vigência;
    // modelado como início/fim por ser o uso prático de uma PT (janela de vigência dentro do dia
    // em que a atividade está liberada) — proposta própria, avisar o usuário se quiser outro formato.
    public TimeSpan? HorarioInicio { get; set; }
    public TimeSpan? HorarioFim { get; set; }

    public DateTime? Validade { get; set; }

    // "Autorização" e "encerramento" (§18) — documento não lista vocabulário literal de status
    // (mesma lacuna já registrada em StatusApr/StatusPgr/StatusControleRisco) — proposta própria.
    public StatusPt Status { get; set; } = StatusPt.EmElaboracao;

    public Guid? AutorizadoPorUsuarioId { get; set; }
    public Usuario? AutorizadoPorUsuario { get; set; }
    public DateTime? DataAutorizacao { get; set; }

    public Guid? EncerradaPorUsuarioId { get; set; }
    public Usuario? EncerradaPorUsuario { get; set; }
    public DateTime? DataEncerramento { get; set; }
    public string? ObservacoesEncerramento { get; set; }

    public ICollection<PermissaoTrabalhoPerigo> Perigos { get; set; } = new List<PermissaoTrabalhoPerigo>();
    public ICollection<PermissaoTrabalhoControle> Controles { get; set; } = new List<PermissaoTrabalhoControle>();
    public ICollection<PermissaoTrabalhoRequisito> Requisitos { get; set; } = new List<PermissaoTrabalhoRequisito>();
    public ICollection<PermissaoTrabalhoResponsavel> Responsaveis { get; set; } = new List<PermissaoTrabalhoResponsavel>();
}

// "Perigos" (§18) — reaproveita o catálogo já existente de Perigo (módulo Riscos), mesmo
// princípio de AprEtapaRisco (não duplica cadastro).
public class PermissaoTrabalhoPerigo : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }

    public Guid PerigoId { get; set; }
    public Perigo? Perigo { get; set; }
}

// "Controles" (§18) — texto livre por controle específico desta PT, complementar aos campos
// ControlesExistentes/ControlesAdicionais já registrados no Risco (não duplica dado), mesmo
// princípio de AprEtapa.MedidasPreventivas.
public class PermissaoTrabalhoControle : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }

    public string Descricao { get; set; } = string.Empty;
}

// "Requisitos" (§18) — checklist de itens que precisam estar atendidos antes da autorização
// (ex.: "sinalização colocada", "extintor disponível", "documentação obrigatória apresentada").
// Não existe no projeto (confirmado por busca no código) nenhuma entidade TipoAutorizacao/
// TipoAutorizacaoRequisito — este checklist é próprio da PT, não um reaproveitamento de outra tabela.
public class PermissaoTrabalhoRequisito : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public bool Atendido { get; set; }
}

// "Responsáveis" (§18) — trabalhadores designados/executantes desta PT. Relação a Trabalhador
// identificável, mesmo padrão de AprResponsavel/RiscoTrabalhadorExposto.
public class PermissaoTrabalhoResponsavel : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
}
