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
                new CertificadoTreinamentoPdfSignatarioModelo("TRABALHADOR DE TESTE", realizacao.AddHours(8)),
                new CertificadoTreinamentoPdfSignatarioModelo("INSTRUTOR DE TESTE", realizacao.AddHours(8).AddMinutes(4)),
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
