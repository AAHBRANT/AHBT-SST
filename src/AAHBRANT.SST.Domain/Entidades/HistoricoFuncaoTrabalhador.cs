using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Uma linha por troca de função do trabalhador. O cadastro guarda só a função atual; sem este
// histórico, a ficha de EPI e a assinatura da OS mostravam a função nova também para o que foi
// entregue/assinado na função anterior. Quem trocou e por qual canal sai da auditoria comum
// (CreatedBy e Origem).
public class HistoricoFuncaoTrabalhador : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public Guid FuncaoAnteriorId { get; set; }
    public Funcao? FuncaoAnterior { get; set; }

    public Guid FuncaoNovaId { get; set; }
    public Funcao? FuncaoNova { get; set; }

    // Momento (UTC) em que a troca foi gravada no SST. Até este instante vale a função anterior.
    public DateTime AlteradaEm { get; set; }
}
