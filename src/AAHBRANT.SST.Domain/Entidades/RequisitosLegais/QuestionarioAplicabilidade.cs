using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Catálogo global de perguntas do questionário de aplicabilidade (mesmo pergunta vale para todas as
// obras — só a resposta é por obra, ver RespostaQuestionarioAplicabilidade). Usado como critério de
// RequisitoLegalCriterio para requisitos cuja aplicabilidade não dá pra derivar só de Perigo/Função/
// Equipamento já cadastrados (ex.: "a obra realiza trabalho em espaço confinado?").
public class ItemQuestionarioAplicabilidade : AuditableEntity
{
    public string Pergunta { get; set; } = string.Empty;
    public string? TextoApoio { get; set; }
}

// Resposta de uma Obra específica a um item do questionário — decisão confirmada com o usuário
// (questionário respondido por obra, já que riscos/atividades variam por obra, mesmo princípio do
// escopo por obra já usado no RBAC Camada 3). Uma linha por (ObraId, ItemQuestionarioAplicabilidadeId)
// — responder de novo atualiza a mesma linha em vez de duplicar (ver
// ResponderQuestionarioAplicabilidadeCommand).
public class RespostaQuestionarioAplicabilidade : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid ItemQuestionarioAplicabilidadeId { get; set; }
    public ItemQuestionarioAplicabilidade? Item { get; set; }

    public bool Resposta { get; set; }
    public string? Observacao { get; set; }
}
