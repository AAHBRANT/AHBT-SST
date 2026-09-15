using AAHBRANT.SST.Domain.Common;

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
}
