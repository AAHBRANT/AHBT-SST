using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// NTAG.md §1/§2 — camada de identificação física (NFC NTAG215/213 e QR Code). Princípio-chave do
// documento (literal): "a tecnologia de leitura NUNCA armazena dados de negócio — a Tag contém
// apenas seu identificador único; o sistema resolve o identificador e carrega a entidade
// correspondente no contexto correto de SST." Por isso esta entidade só guarda Uid + o vínculo
// polimórfico (EntidadeVinculadaTipo/Id) — nunca dado de negócio da área/ativo/trabalhador.
public class TagIdentificacao : AuditableEntity
{
    public string Uid { get; set; } = string.Empty;
    public TipoTag Tipo { get; set; }
    public StatusTag Status { get; set; } = StatusTag.Disponivel;

    public TipoEntidadeVinculada? EntidadeVinculadaTipo { get; set; }
    public Guid? EntidadeVinculadaId { get; set; }
}
