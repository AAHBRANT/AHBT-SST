using System.Text.RegularExpressions;
using AAHBRANT.SST.Application.Dds;
using AAHBRANT.SST.Infrastructure.Documentos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Registro Fotográfico do DDS (05/10): as 3 fotos de evidência saem no PDF, entre a lista de
// presença e a assinatura do responsável.
public class DdsPdfServiceTests
{
    static DdsPdfServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public void Gerar_ComTresFotos_IncluiSecaoRegistroFotograficoEmUmaPagina()
    {
        var pdf = new DdsPdfService().Gerar(CriarModelo(comFotos: true));

        Assert.Equal(1, ContarPaginas(pdf));
        // Salva uma cópia para conferência visual quando a variável de ambiente estiver definida.
        var destino = Environment.GetEnvironmentVariable("DDS_PDF_AMOSTRA");
        if (!string.IsNullOrEmpty(destino)) File.WriteAllBytes(destino, pdf);
    }

    [Fact]
    public void Gerar_SemFotos_NaoQuebra()
    {
        var pdf = new DdsPdfService().Gerar(CriarModelo(comFotos: false));

        Assert.True(pdf.Length > 0);
    }

    // 05/11: DDS completo — lista com Função · CBO e selo na linha e o Certificado de Assinaturas no fim.
    [Fact]
    public void Gerar_ComSeloECertificado_DdsCompleto()
    {
        var cert = CertificadoAssinaturasPdfTests.ModeloTreinamento();
        var itens = cert.Itens.Select((i, n) => i with
        {
            Descricao = n == cert.Itens.Count - 1 ? "DDS — assinatura do responsável" : "DDS — presença do participante",
        }).ToList();
        var painel = new AAHBRANT.SST.Application.Assinatura.PainelCertificado("DDS", "Diálogo Diário de Segurança · 05/11/2026",
            new List<AAHBRANT.SST.Application.Assinatura.SecaoPainelCertificado>
            {
                new("DDS", new List<AAHBRANT.SST.Application.Assinatura.LinhaPainelCertificado>
                    { new("Data", "05/11/2026"), new("Nº", "DDS-0142"), new("Tema(s)", "Trabalho em altura") }),
                new("Responsável", new List<AAHBRANT.SST.Application.Assinatura.LinhaPainelCertificado>
                    { new("Nome", "Gilton Miranda"), new("Função", "Técnico de Segurança") }),
            });
        var certificado = cert with
        {
            NomeDocumento = "DDS", Subtitulo = "DDS · 05/11/2026 · CONSORCIO PONTE RIO CUIA", Itens = itens, PainelGeral = painel,
        };
        var t0 = new DateTime(2026, 11, 5, 10, 12, 0, DateTimeKind.Utc);
        AAHBRANT.SST.Application.Assinatura.SeloAssinaturaPdf Selo(string nome, string funcao, string cod) => new(nome, funcao, cod);
        var modelo = CriarModelo(comFotos: true) with
        {
            ObraNome = "CONSORCIO PONTE RIO CUIA",
            ResponsavelNome = "Gilton Miranda",
            ResponsavelFuncao = "Técnico de Segurança",
            ResponsavelAssinadoEm = t0.AddMinutes(6),
            ResponsavelMetodo = AAHBRANT.SST.Domain.Enums.MetodoAutenticacaoAssinatura.SessaoLogada,
            Participantes = new[]
            {
                new DdsPdfParticipanteModelo("Alan Franklin dos Santos", t0, AAHBRANT.SST.Domain.Enums.MetodoAutenticacaoAssinatura.ReconhecimentoFacial,
                    "Ajudante Geral · CBO 7170-20", Selo("Alan Franklin dos Santos", "Ajudante Geral · CBO 7170-20", "4B88D0A1C93E7F20A1B2C3D4E5F60718")),
                new DdsPdfParticipanteModelo("Carlos Souza Lima", t0.AddMinutes(2), AAHBRANT.SST.Domain.Enums.MetodoAutenticacaoAssinatura.Biometria,
                    "Pedreiro · CBO 7152-10", Selo("Carlos Souza Lima", "Pedreiro · CBO 7152-10", "B02C11AA7E11B02C11AA7E11")),
                new DdsPdfParticipanteModelo("José Pereira", t0.AddMinutes(4), AAHBRANT.SST.Domain.Enums.MetodoAutenticacaoAssinatura.ReconhecimentoFacial,
                    "Carpinteiro · CBO 7155-05", Selo("José Pereira", "Carpinteiro · CBO 7155-05", "A71E09D4A71E09D4A71E09D4")),
            },
            Certificado = certificado,
        };

        var pdf = new DdsPdfService().Gerar(modelo);

        Assert.True(pdf.Length > 1000);
        var pasta = Environment.GetEnvironmentVariable("FICHA_PDF_SAIDA");
        if (!string.IsNullOrEmpty(pasta)) { Directory.CreateDirectory(pasta); File.WriteAllBytes(Path.Combine(pasta, "dds-completo.pdf"), pdf); }
    }

    private static DdsPdfModelo CriarModelo(bool comFotos)
    {
        var fotos = comFotos
            ? new[]
            {
                new DdsPdfFotoModelo(Jpeg(200, 120, 60), new DateTimeOffset(2026, 10, 5, 10, 5, 0, TimeSpan.Zero), -180, "Frente de serviço, bloco B", -7.1150, -34.8610),
                new DdsPdfFotoModelo(Jpeg(60, 120, 200), new DateTimeOffset(2026, 10, 5, 10, 6, 0, TimeSpan.Zero), -180, "Equipe reunida", -7.1150, -34.8610),
                new DdsPdfFotoModelo(Jpeg(60, 180, 90), null, null, "Quadro do tema do dia", null, null),
            }
            : null;

        return new DdsPdfModelo(
            "Obra Exemplo", null, new DateTime(2026, 10, 5), "Responsável Exemplo",
            new[] { new DdsPdfTemaModelo("Trabalho em altura", "Queda de nível", "Trabalho acima de 2 m", "Lesões graves", "Cinto e linha de vida", null) },
            null, null,
            Array.Empty<(string, bool)>(),
            new[]
            {
                new DdsPdfParticipanteModelo("João da Silva", new DateTime(2026, 10, 5, 10, 12, 0, DateTimeKind.Utc)),
                new DdsPdfParticipanteModelo("Marcos Pereira", new DateTime(2026, 10, 5, 10, 13, 0, DateTimeKind.Utc)),
                new DdsPdfParticipanteModelo("Carlos Souza", new DateTime(2026, 10, 5, 10, 14, 0, DateTimeKind.Utc)),
            },
            "DDS-0000", new string('a', 64), "https://exemplo.local/validar/x", Jpeg(0, 0, 0), true,
            Fotos: fotos);
    }

    private static byte[] Jpeg(byte r, byte g, byte b)
    {
        using var imagem = new Image<Rgb24>(800, 600, new Rgb24(r, g, b));
        using var ms = new MemoryStream();
        imagem.Save(ms, new JpegEncoder());
        return ms.ToArray();
    }

    private static int ContarPaginas(byte[] pdf)
    {
        var texto = System.Text.Encoding.Latin1.GetString(pdf);
        return Regex.Matches(texto, @"/Count (\d+)").Select(m => int.Parse(m.Groups[1].Value)).Max();
    }
}
