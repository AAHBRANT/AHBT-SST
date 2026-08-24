using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Programa de Gerenciamento de Riscos (Base de Conhecimento §16). Componentes literais do
// documento: identificação da organização; caracterização das atividades; inventário de riscos;
// classificação dos riscos; medidas de prevenção; plano de ação; acompanhamento; revisão; evidências.
// "Caracterização das atividades" / "inventário de riscos" / "classificação dos riscos" / "medidas
// de prevenção" NÃO viram tabela nova aqui — são consultas sobre Atividade/Risco/NivelRisco/
// ControlesExistentes|Adicionais já existentes (mesmo padrão de InventarioRisco da Fase D), evitando
// duplicar dado. "Evidências" reaproveita a tabela polimórfica Evidencia (EntidadeTipo = nameof(Pgr)).
// "Plano de ação" e "revisão" são as únicas partes que exigem entidade própria (PlanoAcaoItem/PgrRevisao).
public class Pgr : AuditableEntity
{
    // "Identificação da organização" (§16) — o PGR é elaborado por obra/estabelecimento.
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    public DateTime DataElaboracao { get; set; }
    public DateTime? DataProximaRevisao { get; set; }

    // Guid? (não Guid): a tabela Usuarios ainda não tem provisionamento real (Entra ID/SSO
    // pendente, Fase A) — mesmo motivo pelo qual Risco.ResponsavelUsuarioId (§14) já é nulável.
    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    // Não há fluxo literal de status do PGR no documento — proposta própria (avisar o usuário
    // se ele quiser outro fluxo), análoga à StatusControleRisco já usada em Risco (§14).
    public StatusPgr Status { get; set; } = StatusPgr.EmElaboracao;

    public ICollection<PlanoAcaoItem> PlanoDeAcao { get; set; } = new List<PlanoAcaoItem>();
    public ICollection<PgrRevisao> Revisoes { get; set; } = new List<PgrRevisao>();
}

// "Plano de ação" (§16) — reaproveita StatusControleRisco (Pendente/EmAndamento/Concluido) em vez
// de duplicar um enum idêntico; cada item referencia opcionalmente o Risco que originou a ação
// ("classificação dos riscos" → ação corretiva), cobrindo também "acompanhamento" (§16) via Status/Prazo.
public class PlanoAcaoItem : AuditableEntity
{
    public Guid PgrId { get; set; }
    public Pgr? Pgr { get; set; }

    public Guid? RiscoId { get; set; }
    public Risco? Risco { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    public DateTime? Prazo { get; set; }
    public DateTime? DataConclusao { get; set; }

    public StatusControleRisco Status { get; set; } = StatusControleRisco.Pendente;
}

// "Revisão" (§16) — histórico formal de revisões do PGR (distinto da TrilhaAuditoria genérica,
// que registra qualquer alteração de qualquer entidade; aqui é o registro de revisão do documento
// PGR em si, com motivo e responsável, exigido explicitamente pelo componente do §16).
public class PgrRevisao : AuditableEntity
{
    public Guid PgrId { get; set; }
    public Pgr? Pgr { get; set; }

    public int NumeroRevisao { get; set; }
    public DateTime DataRevisao { get; set; }
    public string Motivo { get; set; } = string.Empty;

    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }
}
