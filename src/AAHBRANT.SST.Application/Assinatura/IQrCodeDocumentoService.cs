namespace AAHBRANT.SST.Application.Assinatura;

public record QrCodeDocumentoResultado(byte[] Png, string UrlValidacao);

// A URL base do frontend é config (Infrastructure), não vaza para o Application — este contrato só
// recebe o token e devolve a imagem pronta + a URL completa (para exibir como texto abaixo do QR no
// comprovante, caso o leitor não consiga escanear). Ver AssinaturaOptions/QrCodeDocumentoService.
public interface IQrCodeDocumentoService
{
    QrCodeDocumentoResultado Gerar(string token);
}
