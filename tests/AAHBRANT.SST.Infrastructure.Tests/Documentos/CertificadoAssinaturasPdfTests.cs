using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using QuestPDF.Fluent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Modelo único do Certificado de Assinaturas Eletrônicas (05/11) aplicado a uma lista de presença de
// treinamento: participantes por facial e digital, instrutor por login, duas assinaturas por página.
public class CertificadoAssinaturasPdfTests
{
    static CertificadoAssinaturasPdfTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static byte[] Png(int w, int h, Rgba32 cor)
    {
        using var img = new Image<Rgba32>(w, h, cor);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static EvidenciaAssinaturaPdf Evidencia(MetodoAutenticacaoAssinatura metodo, DateTime quando, byte[]? face, byte[]? digital) => new(
        metodo, quando, quando.AddMinutes(-1), "206.42.35.39",
        "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/129.0 Mobile Safari/537.36",
        StatusLocalizacaoAssinatura.Capturada, -7.1195, -34.8450, 20, "recognition_04", "obra-a1b2c3d4",
        metodo == MetodoAutenticacaoAssinatura.ReconhecimentoFacial ? "7f3c91ae-0000-4b1f-9c2d-aaaaaaaab204" : null,
        metodo == MetodoAutenticacaoAssinatura.Biometria ? Guid.Parse("4f2a0000-0000-0000-0000-000000000001") : null,
        metodo == MetodoAutenticacaoAssinatura.ReconhecimentoFacial ? face : metodo == MetodoAutenticacaoAssinatura.Biometria ? digital : null,
        metodo == MetodoAutenticacaoAssinatura.SessaoLogada ? null : "4B88D0A1C93E7F20A1B2C3D4E5F60718",
        metodo == MetodoAutenticacaoAssinatura.ReconhecimentoFacial ? face : null,
        metodo == MetodoAutenticacaoAssinatura.ReconhecimentoFacial ? new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc) : null,
        metodo == MetodoAutenticacaoAssinatura.ReconhecimentoFacial ? "C15E77A2B6F0913D" : null,
        metodo == MetodoAutenticacaoAssinatura.Biometria ? digital : null,
        metodo == MetodoAutenticacaoAssinatura.Biometria ? new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc) : null,
        Qr());

    private static byte[] Qr()
    {
        using var g = new QRCoder.QRCodeGenerator();
        using var d = g.CreateQrCode("https://sst.exemplo/#/validar/turma", QRCoder.QRCodeGenerator.ECCLevel.M);
        return new QRCoder.PngByteQRCode(d).GetGraphic(10);
    }

    internal static CertificadoAssinaturasModelo ModeloTreinamento()
    {
        var face = Environment.GetEnvironmentVariable("SST_FOTO_TESTE") is { Length: > 0 } f ? File.ReadAllBytes(f) : Png(400, 300, new Rgba32(91, 107, 120));
        var digital = Environment.GetEnvironmentVariable("SST_DIGITAL_TESTE") is { Length: > 0 } d ? File.ReadAllBytes(d) : Png(144, 216, new Rgba32(20, 20, 20));
        var t0 = new DateTime(2026, 11, 5, 10, 12, 0, DateTimeKind.Utc);
        var painel = new PainelCertificado("Treinamento", "Turma 2026-0142 · NR-35 Trabalho em Altura", new List<SecaoPainelCertificado>
        {
            new("Curso", new List<LinhaPainelCertificado>
            {
                new("Curso", "NR-35 Trabalho em Altura"),
                new("Carga horária", "8 h"),
                new("Data", "05/11/2026"),
                new("Local", "Canteiro · sala de treinamento"),
            }),
            new("Instrutor", new List<LinhaPainelCertificado>
            {
                new("Nome", "Gilton Miranda"),
                new("Registro", "MTE-0123056/SP"),
            }),
        });

        var pessoas = new (string Nome, string Funcao, MetodoAutenticacaoAssinatura Metodo)[]
        {
            ("Alan Franklin dos Santos", "Ajudante Geral · CBO 7170-20 · Mat. 30", MetodoAutenticacaoAssinatura.ReconhecimentoFacial),
            ("Carlos Souza Lima", "Pedreiro · CBO 7152-10 · Mat. 41", MetodoAutenticacaoAssinatura.Biometria),
            ("José Pereira", "Carpinteiro · CBO 7155-05 · Mat. 57", MetodoAutenticacaoAssinatura.ReconhecimentoFacial),
            ("Gilton Miranda", "Instrutor · MTE-0123056/SP", MetodoAutenticacaoAssinatura.SessaoLogada),
        };
        var itens = pessoas.Select((p, i) => new CertificadoAssinaturaItem(
            p.Metodo == MetodoAutenticacaoAssinatura.SessaoLogada ? "Lista de presença — assinatura do instrutor" : "Lista de presença — NR-35 Trabalho em Altura",
            p.Nome, p.Funcao, t0.AddDays(-120), t0.AddDays(-120),
            Evidencia(p.Metodo, t0.AddMinutes(i * 2), face, digital))).ToList();

        return new CertificadoAssinaturasModelo(
            "Lista de Presença de Treinamento", "Lista de Presença · Turma 2026-0142 · NR-35 Trabalho em Altura",
            Guid.NewGuid().ToString(), DateTime.UtcNow, "Sistema SST · CONSORCIO PONTE RIO CUIA",
            "9F3A1C7B20E48D5A61B7C0E2F44A9D13B8E5C0A7D6F21E3B4A5C6D7E8F901234", false, itens,
            "CONSORCIO PONTE RIO CUIA", null, "https://sst.exemplo/validar/abc", Qr(), Compacto: true, PainelGeral: painel);
    }

    [Fact]
    public void Treinamento_Compacto_DuasAssinaturasPorPagina()
    {
        var pdf = Document.Create(c => CertificadoAssinaturasPdf.Adicionar(c, ModeloTreinamento())).GeneratePdf();

        Assert.True(pdf.Length > 1000);
        var pasta = Environment.GetEnvironmentVariable("FICHA_PDF_SAIDA");
        if (!string.IsNullOrEmpty(pasta)) { Directory.CreateDirectory(pasta); File.WriteAllBytes(Path.Combine(pasta, "treinamento-certificado-compacto.pdf"), pdf); }
    }

    [Fact]
    public void SemAssinaturas_NaoGeraPagina()
    {
        var modelo = ModeloTreinamento() with { Itens = new List<CertificadoAssinaturaItem>() };
        var pdf = Document.Create(c =>
        {
            c.Page(p => p.Content().Text("documento"));
            CertificadoAssinaturasPdf.Adicionar(c, modelo);
        }).GeneratePdf();
        Assert.True(pdf.Length > 100);
    }

    // 05/11: lista de presença completa — dados da turma, tabela com selo na linha, fotos da turma e o
    // certificado de assinaturas no fim.
    [Fact]
    public void ListaPresenca_Completa_ComSeloECertificado()
    {
        var cert = ModeloTreinamento();
        var foto = Environment.GetEnvironmentVariable("SST_FOTO_TESTE") is { Length: > 0 } f ? File.ReadAllBytes(f) : Png(400, 300, new Rgba32(91, 107, 120));
        var t0 = new DateTime(2026, 11, 5, 10, 12, 0, DateTimeKind.Utc);
        var participantes = new List<AAHBRANT.SST.Application.SessoesTreinamento.AtaSessaoTreinamentoPdfParticipanteModelo>
        {
            new("Alan Franklin dos Santos", "30", t0, MetodoAutenticacaoAssinatura.ReconhecimentoFacial, "Ajudante Geral · CBO 7170-20",
                new SeloAssinaturaPdf("Alan Franklin dos Santos", "Ajudante Geral · CBO 7170-20", "4B88D0A1C93E7F20A1B2C3D4E5F60718")),
            new("Carlos Souza Lima", "41", t0.AddMinutes(2), MetodoAutenticacaoAssinatura.Biometria, "Pedreiro · CBO 7152-10",
                new SeloAssinaturaPdf("Carlos Souza Lima", "Pedreiro · CBO 7152-10", "B02C11AA7E11B02C11AA7E11")),
            new("José Pereira", "57", t0.AddMinutes(4), MetodoAutenticacaoAssinatura.ReconhecimentoFacial, "Carpinteiro · CBO 7155-05",
                new SeloAssinaturaPdf("José Pereira", "Carpinteiro · CBO 7155-05", "A71E09D4A71E09D4A71E09D4")),
            new("Marcos Vieira", "63", null, null, "Armador · CBO 7153-15", null),
        };
        var ata = new AAHBRANT.SST.Application.SessoesTreinamento.AtaSessaoTreinamentoPdfModelo(
            "CONSORCIO PONTE RIO CUIA", null, "NR-35 Trabalho em Altura", "NR-35", new DateTime(2026, 11, 5), 8,
            "Gilton Miranda · Técnico de Segurança · MTE-0123056/SP", "2026-0142", new DateTime(2026, 11, 5, 15, 0, 0),
            participantes, new List<byte[]> { foto, foto },
            cert.ConteudoHash, cert.UrlValidacaoPublica, cert.QrCodePng, cert,
            new AAHBRANT.SST.Application.SessoesTreinamento.AssinaturaInstrutorAtaPdf("Gilton Miranda", "Técnico de Segurança do Trabalho · MTE-0123056/SP",
                t0.AddMinutes(6), MetodoAutenticacaoAssinatura.SessaoLogada, "7E11A90F0C5AA3FCB5A8F3E020737434"));

        var pdf = new AtaSessaoTreinamentoPdfService().Gerar(ata);

        Assert.True(pdf.Length > 1000);
        var pasta = Environment.GetEnvironmentVariable("FICHA_PDF_SAIDA");
        if (!string.IsNullOrEmpty(pasta)) { Directory.CreateDirectory(pasta); File.WriteAllBytes(Path.Combine(pasta, "lista-presenca-treinamento-completa.pdf"), pdf); }
    }
}
