using AAHBRANT.SST.Application.Aprs;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// APR por biometria (05/11): envolvidos com Função · CBO e selo, blocos de elaboração/supervisão com a
// assinatura biométrica, registro antigo por clique marcado "sem biometria" e o certificado no fim.
public class AprPdfServiceTests
{
    static AprPdfServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public void Gerar_AprCompleta_ComSeloBlocosECertificado()
    {
        var cert = CertificadoAssinaturasPdfTests.ModeloTreinamento();
        var itens = cert.Itens.Select((i, n) => i with
        {
            Descricao = n switch { 0 => "APR — assinatura da elaboração", 1 => "APR — assinatura da supervisão", _ => "APR — ciência do envolvido" },
        }).ToList();
        var painel = new PainelCertificado("APR", "APR APR-2026-0031 · Montagem de fôrma da laje", new List<SecaoPainelCertificado>
        {
            new("Atividade", new List<LinhaPainelCertificado> { new("Atividade", "Montagem de fôrma da laje"), new("Local", "Bloco B · 3º pavimento"), new("Data", "05/11/2026") }),
            new("Responsáveis", new List<LinhaPainelCertificado> { new("Elaboração", "Alan Franklin dos Santos"), new("Supervisão", "Carlos Souza Lima"), new("Envolvidos", "3") }),
        });
        var certificado = cert with { NomeDocumento = "APR", Subtitulo = "APR APR-2026-0031 · Montagem de fôrma da laje", Itens = itens, PainelGeral = painel };

        var t0 = new DateTime(2026, 11, 5, 10, 12, 0, DateTimeKind.Utc);
        var modelo = new AprPdfModelo(
            "APR-2026-0031", "CONSORCIO PONTE RIO CUIA", null, "Montagem de fôrma da laje", "Bloco B · 3º pavimento",
            "Serra circular, furadeira", "PGR rev. 03", new DateTime(2026, 11, 5),
            new[]
            {
                new AprPdfEnvolvido("José Pereira", "Carpinteiro · CBO 7155-05", true, t0.AddMinutes(4), MetodoAutenticacaoAssinatura.ReconhecimentoFacial,
                    new SeloAssinaturaPdf("José Pereira", "Carpinteiro · CBO 7155-05", "A71E09D4A71E09D4A71E09D4")),
                new AprPdfEnvolvido("Marcos Vieira", "Armador · CBO 7153-15", true, t0.AddDays(-3), MetodoAutenticacaoAssinatura.SessaoLogada),
                new AprPdfEnvolvido("Pedro Alves", "Servente · CBO 7170-20", false),
            },
            new[]
            {
                new AprPdfRiscoLinha("Içamento das peças", "Queda de material", "Peça mal amarrada", "Contusão", "Equipe da laje", 3, 4, NivelRiscoApr.Alto,
                    "Isolar a área, amarração dupla", "Encarregado", 1, 4, NivelRiscoApr.Moderado),
            },
            new AprPdfAssinatura("Alan Franklin dos Santos", "Técnico de Segurança", t0, MetodoAutenticacaoAssinatura.ReconhecimentoFacial, "4B88D0A1C93E7F20A1B2C3D4E5F60718"),
            new AprPdfAssinatura("Carlos Souza Lima", "Encarregado de Obras", t0.AddMinutes(2), MetodoAutenticacaoAssinatura.Biometria, "B02C11AA7E11B02C11AA7E11"),
            cert.ConteudoHash, cert.UrlValidacaoPublica, cert.QrCodePng, true, certificado);

        var pdf = new AprPdfService().Gerar(modelo);

        Assert.True(pdf.Length > 1000);
        var pasta = Environment.GetEnvironmentVariable("FICHA_PDF_SAIDA");
        if (!string.IsNullOrEmpty(pasta)) { Directory.CreateDirectory(pasta); File.WriteAllBytes(Path.Combine(pasta, "apr-completa.pdf"), pdf); }
    }
}
