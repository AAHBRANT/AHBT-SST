using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.PermissoesTrabalho;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// PT (05/11): equipe com Função · CBO e selo na linha; o certificado de assinaturas é um PDF à parte.
public class PtPdfServiceTests
{
    static PtPdfServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public void Gerar_PtComSelo_ECertificadoAParte()
    {
        var cert = CertificadoAssinaturasPdfTests.ModeloTreinamento();
        var certificado = cert with
        {
            NomeDocumento = "Permissão de Trabalho",
            Subtitulo = "PT PT-0012 · Montagem de forma no pilar P3",
            Itens = cert.Itens.Take(3).Select(i => i with { Descricao = "PT — ciência da equipe executante" }).ToList(),
            PainelGeral = new PainelCertificado("PT", "PT PT-0012 · Montagem de forma no pilar P3", new List<SecaoPainelCertificado>
            {
                new("Atividade", new List<LinhaPainelCertificado> { new("Atividade", "Montagem de forma no pilar P3"), new("Local", "Eixo 4 — pilar P3"), new("Data", "05/11/2026") }),
                new("Liberação", new List<LinhaPainelCertificado> { new("Autorizada por", "Gilton Pereira Miranda"), new("Resp. execução", "Ana Almeida Pereira"), new("Equipe", "4") }),
            }),
        };
        var t0 = new DateTime(2026, 11, 5, 10, 12, 0, DateTimeKind.Utc);
        SeloAssinaturaPdf Selo(string n, string f, string c) => new(n, f, c);
        var modelo = new PtPdfModelo(
            "PT-0012", "CONSORCIO PONTE RIO CUIA", null, "Montagem de forma no pilar P3", "Eixo 4 — pilar P3", "Construtora X",
            new DateTime(2026, 11, 5), new TimeSpan(7, 0, 0), new TimeSpan(17, 0, 0), new DateTime(2026, 11, 5),
            "Ana Almeida Pereira", "Paulo Henrique",
            new List<PtPdfItemBinario>(), new List<PtPdfTipoTrabalho>(), new List<PtPdfVerificacao>(), new List<PtPdfEpi>(),
            null, new List<string>(), null, new List<PtPdfRiscoCritico>(),
            new PtPdfAssinatura("Gilton Pereira Miranda", t0.AddMinutes(-20), MetodoAutenticacaoAssinatura.SessaoLogada, "Técnico de Segurança"),
            new PtPdfAssinatura("Ana Almeida Pereira", t0.AddMinutes(-15), MetodoAutenticacaoAssinatura.SessaoLogada, "Encarregada de Obras"),
            null, null, null,
            new List<PtPdfEnvolvido>
            {
                new("Alan Franklin dos Santos", "Ajudante Geral · CBO 7170-20", true, t0, MetodoAutenticacaoAssinatura.ReconhecimentoFacial,
                    Selo("Alan Franklin dos Santos", "Ajudante Geral · CBO 7170-20", "4B88D0A1C93E7F20A1B2C3D4E5F60718")),
                new("Carlos Souza Lima", "Pedreiro · CBO 7152-10", true, t0.AddMinutes(2), MetodoAutenticacaoAssinatura.Biometria,
                    Selo("Carlos Souza Lima", "Pedreiro · CBO 7152-10", "B02C11AA7E11B02C11AA7E11")),
                new("José Pereira", "Carpinteiro · CBO 7155-05", true, t0.AddMinutes(4), MetodoAutenticacaoAssinatura.ReconhecimentoFacial,
                    Selo("José Pereira", "Carpinteiro · CBO 7155-05", "A71E09D4A71E09D4A71E09D4")),
                new("Pedro Alves", "Servente · CBO 7170-20", false),
            },
            cert.ConteudoHash, cert.UrlValidacaoPublica, cert.QrCodePng, true);

        var pdf = new PtPdfService().Gerar(modelo);
        var pdfCertificado = new CertificadoAssinaturasPdfService().Gerar(certificado);

        Assert.True(pdf.Length > 1000);
        Assert.True(pdfCertificado.Length > 1000);
        var pasta = Environment.GetEnvironmentVariable("FICHA_PDF_SAIDA");
        if (!string.IsNullOrEmpty(pasta))
        {
            Directory.CreateDirectory(pasta);
            File.WriteAllBytes(Path.Combine(pasta, "pt-completa.pdf"), pdf);
            File.WriteAllBytes(Path.Combine(pasta, "pt-certificado-assinaturas.pdf"), pdfCertificado);
        }
    }
}
