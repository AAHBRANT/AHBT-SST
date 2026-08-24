using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Gestão documental (Base de Conhecimento §31, linhas 764-789). Campos literais do documento:
// nome; tipo; categoria; origem; responsável; versão; validade; data de emissão; data de revisão;
// requisito relacionado; obra; setor; status; arquivo; histórico.
// Status literal: Rascunho → Em aprovação → Vigente → Obsoleto → Cancelado.
//
// Decisões não-literais assumidas (a confirmar com o usuário se ele quiser outro comportamento):
// - "origem" (§31) precisou ser renomeado para OrigemDocumento: AuditableEntity já expõe uma
//   propriedade própria chamada Origem (OrigemRegistro: Manual/Importacao/Ocr/IntegracaoGraph, sobre
//   como o REGISTRO entrou no sistema) — mesmo conflito de nome já resolvido em
//   NaoConformidade.OrigemDeteccao. OrigemDocumento aqui é texto livre (ex.: "Elaboração interna",
//   "Contratada", "Órgão fiscalizador") — o documento não define um vocabulário fechado.
// - "tipo" e "categoria" (§31) são texto livre — o documento não lista um vocabulário fechado (ex.:
//   tipo poderia ser "Procedimento"/"Ficha"/"Laudo"; categoria poderia ser "SST"/"Ambiental"), então
//   evitamos inventar um enum fechado que o documento não define.
// - "arquivo" (§31) vira um campo de texto (nome/caminho do arquivo), não upload real — mesmo gap
//   pré-existente já registrado em RequisitoLegal.Evidencia e NaoConformidade: não há armazenamento
//   real de arquivo binário em nenhum módulo do sistema ainda.
// - "requisito relacionado" (§31) é modelado como FK opcional para RequisitoLegal (Matriz Legal,
//   §32) — a leitura mais direta do termo é ligar o documento ao requisito legal que ele atende.
// - "obra" e "setor" (§31) são FKs opcionais (nullable): documento pode ser específico de uma
//   obra/setor ou, se nulo, global — mesmo padrão de escopo já usado em RequisitoLegal.ObraId.
// - "versão" (§31) é texto livre (ex.: "1.0", "Rev. 2") — o documento não define um formato. É a
//   versão ATUAL/vigente do documento; o "histórico" (abaixo) guarda as versões anteriores.
// - "data de revisão" (§31, singular) é interpretada como a data da revisão vigente/mais recente
//   do documento (DataRevisao) — distinta de cada registro individual do histórico.
// - Reclassificação de status é direta (mesmo padrão de AtualizarStatusRequisitoLegalCommand),
//   sem bloqueio preventivo sequencial: um documento em "Vigente" pode ir direto para "Cancelado"
//   (ex.: revogação), sem precisar passar por "Obsoleto" — reflete o funcionamento real de controle
//   documental (qualquer estado pode ser cancelado), diferente do fluxo linear de PT/APR/NC/Acidente.
// - "histórico" (§31) vira a entidade filha DocumentoRevisao, seguindo o mesmo padrão de PgrRevisao
//   (§16): número de revisão, data, motivo e responsável — distinto dos campos de auditoria técnica
//   (CreatedAtUtc/UpdatedAtUtc) de AuditableEntity, que não registram motivo de negócio.
public class DocumentoGestao : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public string? Categoria { get; set; }
    public string? OrigemDocumento { get; set; }

    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    public string? Versao { get; set; }
    public DateTime? Validade { get; set; }
    public DateTime DataEmissao { get; set; }
    public DateTime? DataRevisao { get; set; }

    public Guid? RequisitoLegalId { get; set; }
    public RequisitoLegal? RequisitoLegal { get; set; }

    public Guid? ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid? SetorId { get; set; }
    public Setor? Setor { get; set; }

    public StatusDocumentoGestao Status { get; set; } = StatusDocumentoGestao.Rascunho;

    public string? Arquivo { get; set; }

    public ICollection<DocumentoRevisao> Revisoes { get; set; } = new List<DocumentoRevisao>();
}

// "Histórico" (§31) — histórico formal de revisões do documento, mesmo padrão de PgrRevisao (§16).
public class DocumentoRevisao : AuditableEntity
{
    public Guid DocumentoId { get; set; }
    public DocumentoGestao? Documento { get; set; }

    public int NumeroRevisao { get; set; }
    public DateTime DataRevisao { get; set; }
    public string Motivo { get; set; } = string.Empty;

    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }
}
