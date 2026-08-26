using AAHBRANT.SST.Application.Assinatura;
using Microsoft.Extensions.Options;
using QRCoder;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Gera o QR de validação pública do documento (docs/Motor-Assinatura-Eletronica.md §5, etapa 10). A rota
// /#/validar/{token} em si (ValidacaoPublicaController + ValidarDocumentoPage) é a etapa 11 — aqui só
// se garante que o comprovante já sai da etapa 10 com um QR/link funcional assim que a página existir,
// sem precisar regenerar PDFs antigos depois.
public class QrCodeDocumentoService : IQrCodeDocumentoService
{
    private readonly AssinaturaOptions _options;

    public QrCodeDocumentoService(IOptions<AssinaturaOptions> options) => _options = options.Value;

    public QrCodeDocumentoResultado Gerar(string token)
    {
        // UrlBaseValidacaoPublica vazia (config ainda não preenchida no ambiente) não deve derrubar a
        // finalização do documento — mesmo espírito de tolerância a config ausente usado em
        // Telegram/Graph/ServiceBus (DependencyInjection.cs); o QR fica com um caminho relativo, que
        // segue válido assim que o link completo for necessário.
        //
        // Prefixo "/#/" obrigatório: o TeamsApp usa HashRouter (App.tsx — evita depender de rota
        // configurada no servidor durante o sideload no Teams), então a rota navegável real da página
        // pública é "/#/validar/{token}" (não "/validar/{token}", nem "/sst/validar/{token}"). Sem o
        // "#/" o link cairia na raiz do servidor com um path que o roteador baseado em hash nunca vê.
        var url = $"{_options.UrlBaseValidacaoPublica.TrimEnd('/')}/#/validar/{token}";

        using var geradorQr = new QRCodeGenerator();
        using var dadosQr = geradorQr.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qrPng = new PngByteQRCode(dadosQr);
        var png = qrPng.GetGraphic(10);

        return new QrCodeDocumentoResultado(png, url);
    }
}
