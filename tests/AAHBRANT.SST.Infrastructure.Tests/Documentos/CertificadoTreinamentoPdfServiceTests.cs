using System.Text;
using System.Text.RegularExpressions;
using AAHBRANT.SST.Application.Treinamentos;
using AAHBRANT.SST.Infrastructure.Documentos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Regressão do certificado com verso: o bloco de assinaturas não cabia na página e o QuestPDF o
// partia ao meio, gerando uma terceira página com uma linha de signatário solta e sem cabeçalho —
// visível em qualquer certificado real com 2+ signatários e foto de turma.
public class CertificadoTreinamentoPdfServiceTests
{
    static CertificadoTreinamentoPdfServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public void Gerar_ComConteudoProgramaticoFotoEDoisSignatarios_SaiEmDuasPaginas()
    {
        var pdf = new CertificadoTreinamentoPdfService().Gerar(CriarModelo());

        Assert.Equal(2, ContarPaginas(pdf));
    }

    [Fact]
    public void Gerar_SemFotoDaTurma_SaiEmDuasPaginas()
    {
        var pdf = new CertificadoTreinamentoPdfService().Gerar(CriarModelo() with { FotoTurma = null });

        Assert.Equal(2, ContarPaginas(pdf));
    }

    // NR-10 tem o conteúdo padrão mais longo do catálogo (15 tópicos) — tem que caber no verso.
    [Fact]
    public void Gerar_ComConteudoPadraoDaNr10_SaiEmDuasPaginas()
    {
        var nr10 = AAHBRANT.SST.Domain.Entidades.CatalogoNormasRegulamentadoras.Obter("NR-10")!;
        var modelo = CriarModelo() with
        {
            NormaReferencia = "NR-10",
            NormaNumero = nr10.Numero,
            NormaTitulo = nr10.Titulo,
            ConteudoProgramatico = nr10.ConteudoProgramaticoTexto,
            CargaHorariaRealizada = 40,
        };

        var pdf = new CertificadoTreinamentoPdfService().Gerar(modelo);
        SalvarExemplo(pdf, "nr10");

        Assert.Equal(2, ContarPaginas(pdf));
    }

    [Fact]
    public void Gerar_ComNormaNumeroETitulo_SaiEmDuasPaginas()
    {
        var pdf = new CertificadoTreinamentoPdfService().Gerar(CriarModelo() with
        {
            NormaNumero = "NR-35",
            NormaTitulo = "Trabalho em Altura",
        });
        SalvarExemplo(pdf, "nr35");

        Assert.Equal(2, ContarPaginas(pdf));
    }

    // Para inspeção visual local: CERTIFICADO_EXEMPLO_DIR=<pasta> dotnet test ...
    private static void SalvarExemplo(byte[] pdf, string nome)
    {
        var pasta = Environment.GetEnvironmentVariable("CERTIFICADO_EXEMPLO_DIR");
        if (!string.IsNullOrWhiteSpace(pasta))
            File.WriteAllBytes(Path.Combine(pasta, $"certificado-{nome}.pdf"), pdf);
    }

    // O PDF traz "/Count N" na árvore de páginas — suficiente para afirmar o número de páginas sem
    // trazer uma dependência de leitor de PDF só para o teste.
    private static int ContarPaginas(byte[] pdf)
    {
        var texto = Encoding.Latin1.GetString(pdf);
        var contagem = Regex.Match(texto, @"/Count\s+(\d+)");
        Assert.True(contagem.Success, "Não foi possível determinar o número de páginas do PDF.");
        return int.Parse(contagem.Groups[1].Value);
    }

    private static CertificadoTreinamentoPdfModelo CriarModelo()
    {
        var realizacao = new DateTime(2026, 9, 18, 8, 0, 0);

        return new CertificadoTreinamentoPdfModelo(
            ObraNome: "Obra de Teste",
            ObraLogoConteudo: null,
            ObraCnpj: "00.000.000/0001-00",
            ObraEndereco: "Rua de Teste, 100",
            ObraCidade: "João Pessoa",
            ObraUf: "PB",
            TrabalhadorNome: "TRABALHADOR DE TESTE",
            TrabalhadorCpfMascarado: "***.456.789-**",
            TrabalhadorRg: "1.234.567 SSP/PB",
            TrabalhadorMatricula: "00123",
            TrabalhadorFuncaoNome: "Pedreiro",
            CursoNome: "Trabalho em Altura",
            NormaReferencia: "NR-35",
            CargaHorariaMinima: 8,
            CargaHorariaRealizada: 8,
            DataRealizacao: realizacao,
            DataValidade: realizacao.AddYears(2),
            InstituicaoInstrutor: "Instrutor de Teste",
            InstrutorRegistroProfissional: "MTE 00.123.456",
            NumeroCertificado: "TESTE-2026-0001",
            Local: "Canteiro de obras",
            // Conteúdo programático completo da NR-35: é o caso real, com 7 tópicos, que estourava
            // a página junto com a foto e as assinaturas.
            ConteudoProgramatico:
                "1. Normas e regulamentos aplicáveis ao trabalho em altura.\n" +
                "2. Análise de risco e condições impeditivas.\n" +
                "3. Riscos potenciais inerentes ao trabalho em altura e medidas de prevenção.\n" +
                "4. Sistemas, equipamentos e procedimentos de proteção coletiva.\n" +
                "5. Equipamentos de proteção individual para trabalho em altura: seleção, inspeção, " +
                "conservação e limitação de uso.\n" +
                "6. Acidentes típicos de trabalho em altura.\n" +
                "7. Condutas em situações de emergência, incluindo noções de técnicas de resgate e " +
                "de primeiros socorros.",
            Signatarios: new[]
            {
                new CertificadoTreinamentoPdfSignatarioModelo("TRABALHADOR DE TESTE", realizacao.AddHours(8), Hash: "3F9A12C488B07D1EAA00BB11CC22DD33EE44FF5566778899AABBCCDDEEFF0011"),
                new CertificadoTreinamentoPdfSignatarioModelo("INSTRUTOR DE TESTE", realizacao.AddHours(8).AddMinutes(4), Hash: "9C01E7B2D4F6A8C0E2B4D6F8A0C2E4B6D8F0A2C4E6B8D0F2A4C6E8B0D2F4A6C8"),
            },
            QrCodeValidacaoPng: ImagemDeTeste(120, 120),
            FotoTurma: ImagemDeTeste(900, 600),
            ConteudoHash: new string('A', 64),
            UrlValidacaoPublica: "https://validar.teste/#/validar/TESTE",
            QrCodePng: ImagemDeTeste(120, 120),
            TemAssinatura: true);
    }

    private static byte[] ImagemDeTeste(int largura, int altura)
    {
        using var imagem = new Image<Rgba32>(largura, altura, new Rgba32(0xEE, 0xEE, 0xEE));
        using var saida = new MemoryStream();
        imagem.Save(saida, new JpegEncoder { Quality = 80 });
        return saida.ToArray();
    }
}
