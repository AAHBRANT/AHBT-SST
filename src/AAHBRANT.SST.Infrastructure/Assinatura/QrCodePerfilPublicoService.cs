using AAHBRANT.SST.Application.TagsIdentificacao;
using Microsoft.Extensions.Options;
using QRCoder;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

public class QrCodePerfilPublicoService : IQrCodePerfilPublicoService
{
    private readonly AssinaturaOptions _options;

    public QrCodePerfilPublicoService(IOptions<AssinaturaOptions> options) => _options = options.Value;

    public QrCodePerfilPublicoResultado Gerar(string uid)
    {
        var url = MontarUrl(uid);

        using var geradorQr = new QRCodeGenerator();
        using var dadosQr = geradorQr.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qrPng = new PngByteQRCode(dadosQr);
        var png = qrPng.GetGraphic(10);

        return new QrCodePerfilPublicoResultado(png, url);
    }

    public string MontarUrl(string uid)
        => $"{_options.UrlBaseValidacaoPublica.TrimEnd('/')}/#/p/{Uri.EscapeDataString(uid)}";
}
