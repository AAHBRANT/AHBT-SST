using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Assinatura;

public record DocumentoAssinaturaPdfSignatarioModelo(string TrabalhadorNome, MetodoAutenticacaoAssinatura Metodo, DateTime AssinadoEm);

// Comprovante de assinatura (quem assinou, quando, por qual método, hash de integridade) — não é uma
// reprodução do conteúdo do documento de origem (ex.: os tópicos do DDS já têm o próprio PDF via
// IDdsPdfService/ExportarDdsPdfQuery). O motor é decoupled de cada módulo (mesmo raciocínio de
// ObterDocumentoQuery), então este PDF só sabe EntidadeTipo/EntidadeId, não o conteúdo de negócio.
public record DocumentoAssinaturaPdfModelo(
    Guid DocumentoAssinaturaId,
    string EntidadeTipo,
    Guid EntidadeId,
    DateTime FinalizadoEm,
    string ConteudoHash,
    IReadOnlyList<DocumentoAssinaturaPdfSignatarioModelo> Signatarios,
    byte[]? QrCodePng = null,
    string? UrlValidacaoPublica = null);

public interface IDocumentoAssinaturaPdfService
{
    byte[] Gerar(DocumentoAssinaturaPdfModelo modelo);
}
