using AAHBRANT.SST.Application.EntregasEpi;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Páginas do log de assinaturas no fim da Ficha de EPI. As páginas da ficha em si não mudam: o log só
// é acrescentado depois delas, e só quando há assinaturas de EPI do funcionário.
public class FichaEpiLogAssinaturasPdfTests
{
    static FichaEpiLogAssinaturasPdfTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static int ContarPaginas(byte[] pdf)
    {
        var texto = System.Text.Encoding.Latin1.GetString(pdf);
        var m = System.Text.RegularExpressions.Regex.Matches(texto, @"/Type\s*/Page[^s]");
        return m.Count;
    }

    // Imagens sintéticas só para o teste (nenhuma é biometria real).
    private static byte[] Rosto(Rgba32 fundo)
    {
        using var img = new Image<Rgba32>(200, 260);
        for (var y = 0; y < 260; y++)
            for (var x = 0; x < 200; x++)
            {
                var dx = (x - 100) / 62.0; var dy = (y - 110) / 78.0;
                img[x, y] = dx * dx + dy * dy < 1 ? new Rgba32(201, 155, 122) : (y > 215 ? new Rgba32(74, 106, 134) : fundo);
            }
        using var ms = new MemoryStream();
        img.Save(ms, new JpegEncoder());
        return ms.ToArray();
    }

    private static byte[] Digital(int semente)
    {
        using var img = new Image<L8>(160, 240);
        for (var y = 0; y < 240; y++)
            for (var x = 0; x < 160; x++)
            {
                var r = Math.Sqrt(Math.Pow((x - 80) * 1.2, 2) + Math.Pow(y - 130, 2));
                var v = Math.Sin(r / 3.2 + semente + Math.Sin(x / 17.0) * 1.5);
                img[x, y] = new L8((byte)(v > 0 ? 235 : 12));
            }
        using var ms = new MemoryStream();
        img.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    private static byte[] Quadrado(Rgba32 cor)
    {
        using var img = new Image<Rgba32>(60, 60, cor);
        using var ms = new MemoryStream();
        img.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    private static LogAssinaturaEpiItem Item(
        TipoLogAssinaturaEpi tipo, string desc, MetodoAutenticacaoAssinatura metodo, DateTime assinadoUtc,
        CupomEntregaEpi? cupom = null, byte[]? evidencia = null, byte[]? fotoCad = null, byte[]? digitalCad = null,
        StatusLocalizacaoAssinatura loc = StatusLocalizacaoAssinatura.NaoInformada, string? ua = null,
        string? modelo = null, string? grupo = null, string? req = null) => new(
        tipo, desc, metodo, assinadoUtc, assinadoUtc.AddMinutes(-2), "206.42.35.39", ua, loc,
        loc == StatusLocalizacaoAssinatura.Capturada ? -7.1195 : null, loc == StatusLocalizacaoAssinatura.Capturada ? -34.8450 : null,
        loc == StatusLocalizacaoAssinatura.Capturada ? 35 : null,
        modelo, grupo, req, metodo == MetodoAutenticacaoAssinatura.Biometria ? Guid.Parse("4f2a0000-0000-0000-0000-000000000001") : null,
        evidencia, evidencia is null ? null : "4B88D0A1C93E7F20A1B2C3D4E5F60718",
        fotoCad, fotoCad is null ? null : new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc), fotoCad is null ? null : "C15E77A2B6F0913D",
        digitalCad, metodo == MetodoAutenticacaoAssinatura.Biometria ? new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc) : null,
        cupom);

    private static FichaEpiPdfModelo Modelo(LogAssinaturasFichaEpi? log, byte[]? fotoTrabalhador)
    {
        var qr = Quadrado(new Rgba32(30, 30, 30));
        return new FichaEpiPdfModelo(
            "CONSORCIO PONTE RIO CUIA", "CONSORCIO PONTE RIO CUIA", "57.622.394/0001-37", null, null,
            "Alan Franklin dos Santos", "***.***.***-77", "30", "Ajudante Geral", "Diurno", new DateTime(2026, 7, 1),
            new List<LinhaEntregaEpiPdf>(), new List<LinhaDevolucaoEpiPdf>(),
            "9F3A1C7B20E48D5A61B7C0E2F44A9D13B8E5C0A7D6F21E3B4A5C6D7E8F901234", "https://sst.exemplo/validar/abc", qr,
            TrabalhadorCpf: "012.345.678-77", TrabalhadorFoto: fotoTrabalhador, Log: log);
    }

    private static LogAssinaturasFichaEpi LogCompleto()
    {
        var t0 = new DateTime(2026, 10, 1, 11, 51, 14, DateTimeKind.Utc);
        var cupom = new CupomEntregaEpi(
            new DateTime(2026, 10, 1), "Entrega inicial", null, "2026-0087", new DateTime(2026, 9, 15), "Carlos Eduardo Lima",
            new List<ItemCupomEpi>
            {
                new("Luva de vaqueta", "Marluvas", 2, "12345", new DateTime(2028, 5, 10), new DateTime(2027, 1, 1), Quadrado(new Rgba32(201, 161, 107))),
                new("Capacete classe B", "Plastcor", 1, "31469", new DateTime(2027, 11, 22), new DateTime(2027, 10, 1), Quadrado(new Rgba32(233, 181, 42))),
            });
        return new LogAssinaturasFichaEpi(
            Guid.NewGuid().ToString(), DateTime.UtcNow, 3, new DateTime(2026, 7, 1, 12, 12, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 12, 12, 0, DateTimeKind.Utc),
            new List<LogAssinaturaEpiItem>
            {
                Item(TipoLogAssinaturaEpi.Entrega, "Entrega de EPI — 2 itens", MetodoAutenticacaoAssinatura.Biometria, t0, cupom,
                    evidencia: Digital(2), digitalCad: Digital(5), loc: StatusLocalizacaoAssinatura.Capturada, ua: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/129.0 Safari/537.36"),
                Item(TipoLogAssinaturaEpi.TermoCompromisso, "Termo de recebimento e compromisso de uso", MetodoAutenticacaoAssinatura.Biometria, t0.AddDays(1)),
                Item(TipoLogAssinaturaEpi.Devolucao, "Devolução de EPI — Luva de vaqueta (qtd 1)", MetodoAutenticacaoAssinatura.ReconhecimentoFacial, t0.AddDays(4),
                    evidencia: Rosto(new Rgba32(91, 107, 120)), fotoCad: Rosto(new Rgba32(122, 106, 90)), loc: StatusLocalizacaoAssinatura.NaoAutorizada,
                    ua: "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/129.0 Mobile Safari/537.36",
                    modelo: "recognition_01", grupo: "obra-a1b2c3d4", req: "7f3c91ae-0000-4b1f-9c2d-aaaaaaaab204"),
            }.OrderBy(i => i.AssinadoEmUtc).ToList());
    }

    [Fact]
    public void Gerar_SemLog_MantemAsPaginasDaFichaSemPaginasExtras()
    {
        var servico = new EntregaEpiPdfService();

        var semLog = servico.Gerar(Modelo(null, null));
        var comLog = servico.Gerar(Modelo(LogCompleto(), null));

        Assert.True(ContarPaginas(comLog) > ContarPaginas(semLog));
    }

    [Fact]
    public void Gerar_ComLog_AcrescentaPaginaDoCertificadoMaisUmaPorAssinatura()
    {
        var servico = new EntregaEpiPdfService();
        var semLog = ContarPaginas(servico.Gerar(Modelo(null, null)));

        var comLog = ContarPaginas(servico.Gerar(Modelo(LogCompleto(), null)));

        Assert.Equal(semLog + 1 + 3, comLog);
    }

    [Fact]
    public void Gerar_ComFotoDoFuncionarioELogCompleto_ProduzPdf()
    {
        var pdf = new EntregaEpiPdfService().Gerar(Modelo(LogCompleto(), Rosto(new Rgba32(122, 106, 90))));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        var destino = Environment.GetEnvironmentVariable("ARQUIVO_PDF_FICHA_LOG");
        if (!string.IsNullOrWhiteSpace(destino)) File.WriteAllBytes(destino, pdf);
    }

    [Fact]
    public void Gerar_FuncionarioSemFoto_NaoQuebra()
    {
        var pdf = new EntregaEpiPdfService().Gerar(Modelo(null, null));

        Assert.True(pdf.Length > 1000);
        var destino = Environment.GetEnvironmentVariable("ARQUIVO_PDF_FICHA_SEMLOG");
        if (!string.IsNullOrWhiteSpace(destino)) File.WriteAllBytes(destino, pdf);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/129.0 Safari/537.36", "Chrome · Windows")]
    [InlineData("Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/129.0 Mobile Safari/537.36", "Chrome · Android (celular)")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 Chrome/129.0 Safari/537.36 Edg/129.0", "Edge · Windows")]
    [InlineData(null, "não registrado (anterior à implantação)")]
    public void Equipamento_DescreveNavegadorESistema(string? userAgent, string esperado)
    {
        Assert.Equal(esperado, LogAssinaturasEpiPdf.Equipamento(userAgent));
    }
}
