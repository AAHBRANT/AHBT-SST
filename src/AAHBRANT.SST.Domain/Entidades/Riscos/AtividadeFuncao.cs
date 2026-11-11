using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Vínculo entre uma atividade do PGR e uma função do GHE (Entrega 4 da OS, 10/11/2026). No PGR gerado
// pela EAP as atividades são etapas da obra inteira; a OS de um trabalhador só deve listar as que a
// função dele executa. A IA sugere (Pendente), o Engenheiro de Segurança decide: Validada = a função
// executa a atividade; Descartada = não executa. Mesma regra das medidas de prevenção: nada é apagado,
// o que é descartado fica no banco para auditoria. Par sem registro = ninguém decidiu ainda. Sem
// navegação para Atividade/Funcao de propósito: INNER JOIN com pai excluído (soft delete) esconde o vínculo.
public class AtividadeFuncao : AuditableEntity
{
    public Guid ObraId { get; set; }

    public Guid AtividadeId { get; set; }

    public Guid FuncaoId { get; set; }

    public StatusMedidaPrevencao Status { get; set; }
    // Nome diferente de AuditableEntity.Origem (OrigemRegistro): aqui é quem propôs o vínculo.
    public OrigemMedidaPrevencao OrigemVinculo { get; set; }
    public string? JustificativaIa { get; set; }

    public Guid? ValidadoPorUsuarioId { get; set; }
    public string? ValidadoPorNome { get; set; }
    public DateTime? ValidadoEmUtc { get; set; }
}
