using AAHBRANT.SST.Application.Inspecoes.Adesivo;
using AAHBRANT.SST.Infrastructure.Documentos;
using UglyToad.PdfPig;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Adesivo de inspeção: tamanho da página, grade 2x4 (só a posição escolhida impressa) e conteúdo do texto.
public class AdesivoInspecaoPdfServiceTests
{
    private const float MmParaPt = 72f / 25.4f;

    public AdesivoInspecaoPdfServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static readonly byte[] LogoPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private static AdesivoInspecaoPdfModelo Modelo(
        SituacaoAdesivo situacao = SituacaoAdesivo.Liberado,
        FormatoAdesivo formato = FormatoAdesivo.A4,
        int posicao = 1,
        DateTime? proxima = null,
        byte[]? logo = null,
        string objeto = "RTK-4B27") => new(
        situacao, objeto, "Caminhão munck", "Veículos", "Obra Beira Rio", logo,
        new DateTime(2026, 11, 17), proxima, "Carlos Eduardo Menezes", "Técnico de Segurança do Trabalho",
        "INSP-2026-001234", "https://app.exemplo/#/p/VEI-abc?i=INSP-2026-001234", formato, posicao);

    private static List<(string Texto, double X, double Y)> Palavras(byte[] pdf, out int paginas, out double largura, out double altura)
    {
        using var documento = PdfDocument.Open(pdf);
        paginas = documento.NumberOfPages;
        var pagina = documento.GetPage(1);
        largura = pagina.Width;
        altura = pagina.Height;
        // Y do PdfPig sobe a partir de baixo; converte para "a partir do topo".
        return pagina.GetWords().Select(w => (w.Text, w.BoundingBox.Left, pagina.Height - w.BoundingBox.Top)).ToList();
    }

    [Fact]
    public void Etiqueta_TemUmaPaginaDoTamanhoDoAdesivo()
    {
        var pdf = new AdesivoInspecaoPdfService().Gerar(Modelo(formato: FormatoAdesivo.Etiqueta));

        var palavras = Palavras(pdf, out var paginas, out var largura, out var altura);

        Assert.Equal(1, paginas);
        Assert.Equal(AdesivoInspecaoPdfService.LarguraEtiquetaMm * MmParaPt, largura, 0.5);
        Assert.Equal(AdesivoInspecaoPdfService.AlturaEtiquetaMm * MmParaPt, altura, 0.5);
        Assert.Contains(palavras, p => p.Texto == "LIBERADO");
        Assert.Contains(palavras, p => p.Texto == "RTK-4B27");
    }

    [Fact]
    public void A4_TemUmaPaginaA4()
    {
        var pdf = new AdesivoInspecaoPdfService().Gerar(Modelo());

        Palavras(pdf, out var paginas, out var largura, out var altura);

        Assert.Equal(1, paginas);
        Assert.Equal(210 * MmParaPt, largura, 0.5);
        Assert.Equal(297 * MmParaPt, altura, 0.5);
    }

    [Fact]
    public void A4_GradeEhDe2ColunasPor4Linhas_Com8Posicoes()
    {
        Assert.Equal(2, AdesivoInspecaoPdfService.Colunas);
        Assert.Equal(4, AdesivoInspecaoPdfService.Linhas);
        Assert.Equal(8, AdesivoInspecaoPdfService.PosicoesPorFolha);
        Assert.Equal(GerarAdesivoInspecaoCommandHandler.PosicoesPorFolha, AdesivoInspecaoPdfService.PosicoesPorFolha);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void A4_ImprimeSoNaPosicaoEscolhida_EOsDemaisFicamEmBranco(int posicao)
    {
        var pdf = new AdesivoInspecaoPdfService().Gerar(Modelo(posicao: posicao));

        var palavras = Palavras(pdf, out _, out _, out _);

        // O identificador aparece uma única vez na folha inteira, e dentro da célula da posição escolhida.
        var ocorrencias = palavras.Where(p => p.Texto == "RTK-4B27").ToList();
        var unica = Assert.Single(ocorrencias);

        var (xMm, yMm) = AdesivoInspecaoPdfService.OrigemCelulaMm(posicao);
        var esquerda = xMm * MmParaPt;
        var direita = (xMm + AdesivoInspecaoPdfService.LarguraEtiquetaMm) * MmParaPt;
        var topo = yMm * MmParaPt;
        var base_ = (yMm + AdesivoInspecaoPdfService.AlturaEtiquetaMm) * MmParaPt;
        Assert.InRange(unica.X, esquerda, direita);
        Assert.InRange(unica.Y, topo, base_);

        // Todo o texto da folha está dentro da célula escolhida (nenhuma outra posição recebe nada).
        Assert.All(palavras, p =>
        {
            Assert.InRange(p.X, esquerda - 1, direita + 1);
            Assert.InRange(p.Y, topo - 1, base_ + 1);
        });
    }

    [Fact]
    public void OrigemDasCelulas_SegueAFolhaDe8Etiquetas()
    {
        // Posição 1: canto superior esquerdo; 2: segunda coluna (passo 101,6 mm); 3: segunda linha (passo 67,7 mm).
        Assert.Equal((4.65f, 13f), AdesivoInspecaoPdfService.OrigemCelulaMm(1));
        Assert.Equal((4.65f + 101.6f, 13f), AdesivoInspecaoPdfService.OrigemCelulaMm(2));
        Assert.Equal((4.65f, 13f + 67.7f), AdesivoInspecaoPdfService.OrigemCelulaMm(3));
        var ultima = AdesivoInspecaoPdfService.OrigemCelulaMm(8);
        Assert.Equal(4.65f + 101.6f, ultima.X, 3);
        Assert.Equal(13f + 3 * 67.7f, ultima.Y, 3);
        // A última linha cabe na folha A4.
        Assert.True(ultima.Y + AdesivoInspecaoPdfService.AlturaEtiquetaMm <= 297f);
    }

    [Theory]
    [InlineData(SituacaoAdesivo.Liberado, "LIBERADO", "LIBERADO PARA USO")]
    [InlineData(SituacaoAdesivo.Inspecionado, "INSPECIONADO", "INSPEÇÃO REALIZADA")]
    [InlineData(SituacaoAdesivo.NaoLiberado, "LIBERADO", "USO BLOQUEADO")]
    public void TextoDoStatus_EOsDadosObrigatorios(SituacaoAdesivo situacao, string palavraTitulo, string subtitulo)
    {
        var pdf = new AdesivoInspecaoPdfService().Gerar(Modelo(situacao, FormatoAdesivo.Etiqueta, proxima: new DateTime(2026, 12, 17)));

        using var documento = PdfDocument.Open(pdf);
        var texto = string.Join(" ", documento.GetPage(1).GetWords().Select(w => w.Text));

        Assert.Contains(palavraTitulo, texto);
        if (situacao == SituacaoAdesivo.NaoLiberado) Assert.Contains("NÃO", texto);
        Assert.Contains(subtitulo, texto);
        Assert.Contains("RTK-4B27", texto);
        Assert.Contains("Obra Beira Rio", texto);
        Assert.Contains("17/11/2026", texto);
        Assert.Contains("17/12/2026", texto);
        Assert.Contains("Carlos Eduardo Menezes", texto);
        Assert.Contains("INSP-2026-001234", texto);
        Assert.Contains("Confira a situação atual", texto);
        Assert.Contains("Inspeção interna de SST. Leia o QR para conferir a situação atual.", texto);
        Assert.DoesNotContain("vencid", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SemPeriodicidade_NaoImprimeProximaInspecao_ExcetoReinspecaoNoNaoLiberado()
    {
        var servico = new AdesivoInspecaoPdfService();

        string Texto(AdesivoInspecaoPdfModelo m)
        {
            using var doc = PdfDocument.Open(servico.Gerar(m));
            return string.Join(" ", doc.GetPage(1).GetWords().Select(w => w.Text));
        }

        var liberado = Texto(Modelo(SituacaoAdesivo.Liberado, FormatoAdesivo.Etiqueta, proxima: null));
        Assert.DoesNotContain("PRÓXIMA", liberado.ToUpperInvariant());

        var bloqueado = Texto(Modelo(SituacaoAdesivo.NaoLiberado, FormatoAdesivo.Etiqueta, proxima: null));
        Assert.Contains("Reinspeção antes do uso", bloqueado);
    }

    [Fact]
    public void LogoDaObra_E_TextosLongos_NaoQuebramOLayout()
    {
        var servico = new AdesivoInspecaoPdfService();
        var longo = new string('W', 120);
        var modelo = Modelo(SituacaoAdesivo.NaoLiberado, FormatoAdesivo.A4, 8, logo: LogoPng, objeto: longo) with
        {
            ObjetoDescricao = longo,
            Tema = "Produtos Químicos e Inflamáveis e outros temas bem longos",
            ObraNome = longo,
            InspetorNome = longo,
            InspetorFuncao = longo,
        };

        var pdf = servico.Gerar(modelo);

        Palavras(pdf, out var paginas, out _, out _);
        Assert.Equal(1, paginas);
    }

    [Fact]
    public void SemLogo_GeraNormalmente()
    {
        var pdf = new AdesivoInspecaoPdfService().Gerar(Modelo(logo: null, formato: FormatoAdesivo.Etiqueta));
        Assert.True(pdf.Length > 1000);
    }

    [Fact]
    public void SaidaOpcionalParaConferenciaVisual()
    {
        var saida = Environment.GetEnvironmentVariable("SST_PDF_SAIDA");
        if (string.IsNullOrEmpty(saida)) return;

        Directory.CreateDirectory(saida);
        var servico = new AdesivoInspecaoPdfService();
        File.WriteAllBytes(Path.Combine(saida, "adesivo-liberado-a4-pos1.pdf"), servico.Gerar(Modelo(SituacaoAdesivo.Liberado, FormatoAdesivo.A4, 1, new DateTime(2026, 12, 17), LogoPng)));
        File.WriteAllBytes(Path.Combine(saida, "adesivo-inspecionado-a4-pos4.pdf"), servico.Gerar(Modelo(SituacaoAdesivo.Inspecionado, FormatoAdesivo.A4, 4, new DateTime(2026, 12, 17), LogoPng, "EXT-PQS-0142")));
        File.WriteAllBytes(Path.Combine(saida, "adesivo-naoliberado-etiqueta.pdf"), servico.Gerar(Modelo(SituacaoAdesivo.NaoLiberado, FormatoAdesivo.Etiqueta, 1, null, LogoPng, "ESC-018")));
    }

    [Fact]
    public void Rodape_AvisoDeExtintor_SoNoTemaExtintores()
    {
        Assert.True(AdesivoInspecaoPdfService.EhExtintor("Extintores"));
        Assert.False(AdesivoInspecaoPdfService.EhExtintor("Andaimes"));

        var andaime = Palavras(new AdesivoInspecaoPdfService().Gerar(Modelo(formato: FormatoAdesivo.Etiqueta)), out _, out _, out _);
        Assert.DoesNotContain(andaime, p => p.Texto == "extintor.");
    }

    [Fact]
    public void ObraSemLogo_UsaLogoAahbrant_SemQuebrar()
    {
        var pdf = new AdesivoInspecaoPdfService().Gerar(Modelo(formato: FormatoAdesivo.Etiqueta, logo: null));
        using var documento = PdfDocument.Open(pdf);
        Assert.NotEmpty(documento.GetPage(1).GetImages());
    }
}
