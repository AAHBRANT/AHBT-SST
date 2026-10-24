using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Catálogo global de materiais de apoio (pôsteres de sinalização, instruções técnicas, etc.) —
// pedido do usuário em 2026-09-09, aba "Documentos & Procedimentos" (Gestão de SST), reservada
// desde a remoção do módulo de Gestão Documental em 28/08. Deliberadamente mais simples que aquele
// módulo antigo: sem workflow de aprovação/versão, sem vínculo com Obra/Setor/RequisitoLegal — só
// nome, categoria (texto livre) e o arquivo em si, guardado como bytes (mesmo padrão já usado em
// Dds.FotoConteudo / InspecaoItemResposta.FotoConteudo), diferente do antigo DocumentoGestao.Arquivo
// que era só um campo de texto (nunca guardava o conteúdo real).
public class MaterialApoio : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Categoria { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();

    // Ordem de Serviço (20/10/2026): procedimentos, ITs e políticas corporativas moram aqui também,
    // com código SGI, revisão e situação. Só documento Vigente entra na OS. Material de apoio antigo
    // continua com Tipo = MaterialApoio e os campos abaixo vazios.
    public TipoDocumentoSst Tipo { get; set; } = TipoDocumentoSst.MaterialApoio;
    public string? Codigo { get; set; }
    public string? Revisao { get; set; }
    public SituacaoDocumentoSst? Situacao { get; set; }
    public string? FormaConsulta { get; set; }
    // Só para política: entra em todas as Ordens de Serviço.
    public bool IncluirEmTodasOs { get; set; }
}
