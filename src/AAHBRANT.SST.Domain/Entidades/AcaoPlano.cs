using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Plano de ação genérico (Base de Conhecimento §26, linhas 645-663). Campos literais do documento:
// descrição; origem; responsável; prioridade; prazo; status; evidência; validação.
//
// Entidade NOVA e SEPARADA de PlanoAcaoItem (Domain/Entidades/Pgr/Pgr.cs) — aquela é específica do
// PGR (PgrId obrigatório) e já está em produção verificada; não foi tocada para não quebrar o
// módulo PGR. Esta (AcaoPlano) é genérica e polimórfica (OrigemTipo/OrigemId, mesmo princípio já
// usado em Evidencia.EntidadeTipo/EntidadeId), pensada para ser reutilizada por Não Conformidade
// (ação corretiva/preventiva, §25) e por módulos futuros (Acidentes, Auditorias) sem precisar criar
// uma tabela de plano de ação por módulo.
//
// Decisões não-literais assumidas:
// - "origem" (§26) aqui é o par polimórfico OrigemTipo/OrigemId (o que gerou esta ação) — proposta
//   própria de modelagem, já que o documento não detalha o formato do campo.
// - "status" reaproveita StatusControleRisco (Pendente/EmAndamento/Concluido), mesmo enum já usado
//   pelo PlanoAcaoItem específico do PGR, em vez de duplicar um vocabulário equivalente.
// - "validação" (§26) vira DataValidacao/ValidadoPorUsuarioId, preenchidos pelo comando
//   ValidarAcaoPlanoCommand — proposta própria para tornar "validação" um dado concreto e não só
//   uma palavra solta na lista de campos do documento.
// - "evidência": mesmo gap pré-existente já registrado em NaoConformidade — a tabela Evidencia
//   existe mas não tem uso real em nenhum módulo do sistema; não foi resolvido nesta fatia.
public class AcaoPlano : AuditableEntity
{
    public string OrigemTipo { get; set; } = string.Empty;
    public Guid OrigemId { get; set; }

    public TipoAcaoPlano Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty;

    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    public PrioridadeAcao Prioridade { get; set; }
    public DateTime? Prazo { get; set; }

    public StatusControleRisco Status { get; set; } = StatusControleRisco.Pendente;
    public DateTime? DataConclusao { get; set; }

    public DateTime? DataValidacao { get; set; }
    public Guid? ValidadoPorUsuarioId { get; set; }
    public Usuario? ValidadoPorUsuario { get; set; }
}
