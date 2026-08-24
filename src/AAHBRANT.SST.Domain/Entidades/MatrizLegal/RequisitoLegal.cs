using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Requisito legal (Base de Conhecimento §32, linhas 792-813). Campos literais do documento:
// código; norma; item; tema; requisito; aplicabilidade; justificativa; evidência; responsável;
// periodicidade; prazo; status; última revisão; próxima revisão.
//
// Decisões não-literais assumidas (a confirmar com o usuário se ele quiser outro comportamento):
// - "evidência" (§32) vira um campo de texto (Evidencia), não a tabela genérica Evidencia
//   (EntidadeTipo/EntidadeId) — mesmo gap pré-existente já registrado em NaoConformidade: a tabela
//   genérica existe mas não tem nenhum controller/uso real em nenhum módulo do sistema.
// - "periodicidade" (§32) é texto livre (ex.: "Anual", "A cada 2 anos") — o documento não define um
//   vocabulário fechado, então evitamos inventar categorias que ele não lista.
// - ObraId (nullable): campo NOVO, não citado no §32. Permite que um requisito seja específico de
//   uma obra ou, se nulo, global (aplicável à empresa como um todo). Proposta própria de modelagem —
//   o documento não detalha esse escopo.
// - "motor de aplicabilidade" (§33: regras automáticas baseadas em características da obra, ex.
//   "trabalho em altura → NR-35") NÃO foi implementado nesta fatia. Exigiria adicionar flags de
//   característica à entidade Obra (schema change em entidade já usada por outros módulos) e o
//   documento não detalha o formato de armazenamento das regras. O campo Aplicabilidade permanece
//   manual (Sim/Não), mesmo padrão do IEligibilityService (ERD.md) — motor concreto fica para fase
//   futura, a avisar o usuário.
public class RequisitoLegal : AuditableEntity
{
    public string Codigo { get; set; } = string.Empty;
    public string Norma { get; set; } = string.Empty;
    public string? Item { get; set; }
    public string Tema { get; set; } = string.Empty;
    public string Requisito { get; set; } = string.Empty;

    public bool Aplicabilidade { get; set; } = true;
    public string? Justificativa { get; set; }
    public string? Evidencia { get; set; }

    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    public string? Periodicidade { get; set; }
    public DateTime? Prazo { get; set; }

    public StatusRequisitoLegal Status { get; set; } = StatusRequisitoLegal.Conforme;

    public DateTime? UltimaRevisao { get; set; }
    public DateTime? ProximaRevisao { get; set; }

    public Guid? ObraId { get; set; }
    public Obra? Obra { get; set; }
}
