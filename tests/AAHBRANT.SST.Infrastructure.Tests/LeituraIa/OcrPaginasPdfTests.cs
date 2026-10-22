using System.Net;
using System.Text;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;
using AAHBRANT.SST.Infrastructure.LeituraIa;
using AAHBRANT.SST.Infrastructure.Tests.Assinatura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.LeituraIa;

// OCR de PDF digitalizado (21/10/2026): a página escaneada vira JPEG reduzido e vai para a IA com a
// mensagem; a resposta (texto transcrito) volta no lugar da página vazia.
public class OcrPaginasPdfTests
{
    public OcrPaginasPdfTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    // "Scanner": uma imagem grande (A4 a ~300 dpi) ocupando a página inteira, sem nenhum texto no PDF.
    private static byte[] PdfDigitalizado(int paginas = 1, bool primeiraComTexto = false)
    {
        using var imagem = new Image<Rgb24>(2480, 3508);
        imagem.ProcessPixelRows(linhas =>
        {
            for (var y = 0; y < linhas.Height; y++)
            {
                var linha = linhas.GetRowSpan(y);
                for (var x = 0; x < linha.Length; x++)
                    linha[x] = (x / 40 + y / 40) % 2 == 0 ? new Rgb24(250, 250, 245) : new Rgb24(30, 30, 30);
            }
        });
        using var jpeg = new MemoryStream();
        imagem.SaveAsJpeg(jpeg);
        var bytesImagem = jpeg.ToArray();

        return Document.Create(doc =>
        {
            for (var i = 0; i < paginas; i++)
            {
                var indice = i;
                doc.Page(p =>
                {
                    p.Size(PageSizes.A4);
                    p.Margin(0);
                    if (indice == 0 && primeiraComTexto)
                        p.Content().Padding(40).Text("CONTRATO DE PRESTAÇÃO DE SERVIÇOS Nº 123/2026 entre AAHBRANT e Montagens Teste Ltda.");
                    else
                        p.Content().Image(bytesImagem).FitArea();
                });
            }
        }).GeneratePdf();
    }

    [Fact]
    public void ExtrairJpegs_PaginaEscaneada_DevolveJpegReduzido()
    {
        var pdf = PdfDigitalizado();

        var imagens = ImagemPaginaPdf.ExtrairJpegs(pdf, new[] { 0 });

        var bytes = Assert.Single(imagens).Value;
        Assert.True(bytes[0] == 0xFF && bytes[1] == 0xD8, "deveria ser JPEG");
        using var img = Image.Load(bytes);
        Assert.True(Math.Max(img.Width, img.Height) <= ImagemPaginaPdf.LadoMaximo);
        Assert.True(Math.Max(img.Width, img.Height) >= 1500, "não deveria reduzir demais (perde a letra miúda)");
    }

    [Fact]
    public void ExtrairJpegs_PaginaSemImagemOuForaDoPdf_FicaDeFora()
    {
        var pdf = PdfDigitalizado(paginas: 2, primeiraComTexto: true);

        var imagens = ImagemPaginaPdf.ExtrairJpegs(pdf, new[] { 0, 1, 7 });

        Assert.Equal(new[] { 1 }, imagens.Keys.ToArray());
    }

    private static (AzureOpenAiOcrPaginasPdf Ocr, List<string> Corpos) CriarOcr(string textoResposta, HttpStatusCode status = HttpStatusCode.OK)
    {
        var corpos = new List<string>();
        var fabrica = new HttpClientFactoryFalso(req =>
        {
            lock (corpos) corpos.Add(req.Content!.ReadAsStringAsync().Result);
            var conteudo = System.Text.Json.JsonSerializer.Serialize(new { texto = textoResposta });
            var json = System.Text.Json.JsonSerializer.Serialize(new { content = new[] { new { type = "text", text = conteudo } } });
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });
        var chat = new AzureOpenAiChatJsonCliente(fabrica,
            Options.Create(new AzureOpenAiOptions()),
            Options.Create(new ClaudeOptions { ApiKey = "chave-de-teste" }),
            NullLogger<AzureOpenAiChatJsonCliente>.Instance);
        return (new AzureOpenAiOcrPaginasPdf(chat, NullLogger<AzureOpenAiOcrPaginasPdf>.Instance), corpos);
    }

    [Fact]
    public async Task Completar_PaginaEscaneada_EnviaImagemEPreencheOTexto()
    {
        var pdf = PdfDigitalizado(paginas: 2, primeiraComTexto: true);
        var (ocr, corpos) = CriarOcr("CLÁUSULA 1 — OBJETO: montagem de estrutura metálica. CNPJ 11.222.333/0001-81");
        var textoPagina1 = "CONTRATO DE PRESTAÇÃO DE SERVIÇOS Nº 123/2026 entre AAHBRANT e Montagens Teste Ltda.";

        var paginas = await ocr.CompletarAsync(pdf, new[] { textoPagina1, "" }, 15, CancellationToken.None);

        Assert.Equal(textoPagina1, paginas[0]); // página com texto não passa pela IA.
        Assert.StartsWith("CLÁUSULA 1", paginas[1]);
        var corpo = Assert.Single(corpos);
        Assert.Contains("\"type\":\"image\"", corpo);
        Assert.Contains("\"media_type\":\"image/jpeg\"", corpo);
    }

    [Fact]
    public async Task Completar_RespeitaOLimiteDePaginas()
    {
        var pdf = PdfDigitalizado(paginas: 3);
        var (ocr, corpos) = CriarOcr("texto lido");

        var paginas = await ocr.CompletarAsync(pdf, new[] { "", "", "" }, 2, CancellationToken.None);

        Assert.Equal(2, corpos.Count);
        Assert.Equal(new[] { "texto lido", "texto lido", "" }, paginas);
    }

    [Fact]
    public async Task Completar_IaFalha_PaginaFicaVaziaSemDerrubarALeitura()
    {
        var pdf = PdfDigitalizado();
        var (ocr, _) = CriarOcr("x", HttpStatusCode.BadRequest);

        var paginas = await ocr.CompletarAsync(pdf, new[] { "" }, 15, CancellationToken.None);

        Assert.Equal(new[] { "" }, paginas);
    }

    [Fact]
    public async Task Completar_PdfComTexto_NaoChamaAIa()
    {
        var (ocr, corpos) = CriarOcr("nunca");
        var texto = new string('t', 100);

        var paginas = await ocr.CompletarAsync(new byte[] { 1, 2, 3 }, new[] { texto }, 15, CancellationToken.None);

        Assert.Equal(new[] { texto }, paginas);
        Assert.Empty(corpos);
    }
}
