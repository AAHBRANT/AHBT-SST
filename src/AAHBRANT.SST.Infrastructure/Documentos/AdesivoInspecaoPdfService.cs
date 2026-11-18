using System.Globalization;
using AAHBRANT.SST.Application.Inspecoes.Adesivo;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Adesivo de inspeção (LIBERADO / INSPECIONADO / NÃO LIBERADO), layout do mockup docs/mockups/adesivo-inspecao-
// 2026-11-17.html adaptado de 100 x 70 mm para a célula padrão de folha de 8 etiquetas (99,1 x 67,7 mm).
//
// Dois formatos: Etiqueta (uma página só do tamanho do adesivo) e A4 (grade 2 colunas x 4 linhas; imprime só a
// posição escolhida e deixa as outras em branco para a folha ser reaproveitada).
//
// ATENÇÃO — medidas da folha A4: partem da referência Avery L7165 (8 etiquetas 99,1 x 67,7 mm). O CÓDIGO REAL DA
// FOLHA PRECISA SER CONFIRMADO COM O FORNECEDOR do papel adesivo; se as medidas forem outras, é só mudar as
// constantes abaixo (nada mais depende delas).
public class AdesivoInspecaoPdfService : IAdesivoInspecaoPdfService
{
    // ---- Medidas da folha/etiqueta (mm) — fáceis de mudar, confirmar com o fornecedor ----
    internal const float LarguraEtiquetaMm = 99.1f;
    internal const float AlturaEtiquetaMm = 67.7f;
    internal const float MargemSuperiorMm = 13f;
    internal const float MargemEsquerdaMm = 4.65f;
    internal const float PassoHorizontalMm = 101.6f;
    internal const float PassoVerticalMm = 67.7f;
    internal const int Colunas = 2;
    internal const int Linhas = 4;
    internal const int PosicoesPorFolha = Colunas * Linhas;

    private const float MmParaPt = 72f / 25.4f;
    private const float AlturaCabecalhoMm = 10.5f;
    private const float AlturaStatusMm = 15.5f;
    private const float AlturaRodapeMm = 6f;

    private const string CorMarca = "#670000";
    // Mockup: gradiente de (a) para (b); o QuestPDF não tem gradiente, então vai a cor do meio.
    private const string CorLiberado = "#059669";
    private const string CorInspecionado = "#0d9488";
    private const string CorNaoLiberado = "#b91c1c";
    private const string CorMolduraNaoLiberado = "#dc2626";

    private static readonly Unit Mm = Unit.Millimetre;

    public byte[] Gerar(AdesivoInspecaoPdfModelo modelo)
    {
        var qrPng = GerarQrPng(modelo.UrlQr);

        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Margin(0);
                pagina.DefaultTextStyle(e => e.FontColor("#111111"));

                if (modelo.Formato == FormatoAdesivo.Etiqueta)
                {
                    pagina.Size(new PageSize(LarguraEtiquetaMm, AlturaEtiquetaMm, Mm));
                    pagina.Content().Element(c => Etiqueta(c, modelo, qrPng));
                }
                else
                {
                    pagina.Size(PageSizes.A4);
                    pagina.Content()
                        .PaddingTop(MargemSuperiorMm, Mm)
                        .PaddingLeft(MargemEsquerdaMm, Mm)
                        .Column(coluna =>
                        {
                            var folgaHorizontal = PassoHorizontalMm - LarguraEtiquetaMm;
                            for (var linha = 0; linha < Linhas; linha++)
                            {
                                var l = linha;
                                coluna.Item().Height(PassoVerticalMm, Mm).Row(row =>
                                {
                                    for (var col = 0; col < Colunas; col++)
                                    {
                                        var posicao = l * Colunas + col + 1;
                                        row.ConstantItem(LarguraEtiquetaMm, Mm).Element(celula =>
                                        {
                                            // Só a posição escolhida é impressa; as outras ficam em branco.
                                            if (posicao == modelo.Posicao)
                                                Etiqueta(celula, modelo, qrPng);
                                        });
                                        if (col < Colunas - 1)
                                            row.ConstantItem(folgaHorizontal, Mm);
                                    }
                                });
                            }
                        });
                }
            });
        });

        return documento.GeneratePdf();
    }

    // Canto superior esquerdo (mm, a partir do canto superior esquerdo da folha) da célula da posição 1..8.
    internal static (float X, float Y) OrigemCelulaMm(int posicao)
    {
        var indice = posicao - 1;
        var linha = indice / Colunas;
        var coluna = indice % Colunas;
        return (MargemEsquerdaMm + coluna * PassoHorizontalMm, MargemSuperiorMm + linha * PassoVerticalMm);
    }

    private static byte[] GerarQrPng(string url)
    {
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(dados);
        return qr.GetGraphic(10);
    }

    private static string CorDaSituacao(SituacaoAdesivo situacao) => situacao switch
    {
        SituacaoAdesivo.NaoLiberado => CorNaoLiberado,
        SituacaoAdesivo.Inspecionado => CorInspecionado,
        _ => CorLiberado,
    };

    private static (string Titulo, string Subtitulo) TextoDaSituacao(SituacaoAdesivo situacao) => situacao switch
    {
        SituacaoAdesivo.NaoLiberado => ("NÃO LIBERADO", "Uso bloqueado"),
        SituacaoAdesivo.Inspecionado => ("INSPECIONADO", "Inspeção realizada"),
        _ => ("LIBERADO", "Liberado para uso"),
    };

    private static string IconeSvg(SituacaoAdesivo situacao) => situacao == SituacaoAdesivo.NaoLiberado
        ? "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"#ffffff\" stroke-width=\"2.4\"><circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"M8 8l8 8M16 8l-8 8\"/></svg>"
        : "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"#ffffff\" stroke-width=\"2.4\"><circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"M7 12.5l3.2 3.2L17 9\"/></svg>";

    private static float Pt(float mm) => mm * MmParaPt;

    private static string Data(DateTime data) => data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static void Etiqueta(IContainer container, AdesivoInspecaoPdfModelo m, byte[] qrPng)
    {
        var naoLiberado = m.Situacao == SituacaoAdesivo.NaoLiberado;
        var cor = CorDaSituacao(m.Situacao);
        var (titulo, subtitulo) = TextoDaSituacao(m.Situacao);

        // 0,2 mm de folga para arredondamento; a moldura do NÃO LIBERADO ocupa 1,3 mm de cada lado.
        var corpoAltura = AlturaEtiquetaMm - AlturaCabecalhoMm - AlturaStatusMm - AlturaRodapeMm - 0.2f - (naoLiberado ? 2.6f : 0f);

        var quadro = container
            .Width(LarguraEtiquetaMm, Mm).Height(AlturaEtiquetaMm, Mm)
            .Background(Colors.White);

        // NÃO LIBERADO tem moldura vermelha ao redor de tudo (a moldura ocupa a borda da etiqueta).
        if (naoLiberado)
            quadro = quadro.Border(1.3f, Mm).BorderColor(CorMolduraNaoLiberado);

        quadro.Column(coluna =>
        {
            // Cabeçalho branco: logo da OBRA; obra sem logo cadastrada usa a da AAHBRANT (decisão do usuário em
            // 18/11/2026) + filete vinho.
            var logoCabecalho = m.ObraLogoConteudo is { Length: > 0 } ? m.ObraLogoConteudo : LogoAahbrant.Value;
            coluna.Item().Height(AlturaCabecalhoMm, Mm).BorderBottom(0.7f, Mm).BorderColor(CorMarca)
                .PaddingHorizontal(3.5f, Mm).Row(linha =>
                {
                    linha.ConstantItem(34, Mm).AlignMiddle().Height(7.6f, Mm).Element(logo =>
                    {
                        if (logoCabecalho is { Length: > 0 })
                            logo.Image(logoCabecalho).FitArea();
                    });
                    linha.RelativeItem().AlignMiddle().AlignRight().Text(m.Tema.ToUpperInvariant())
                        .FontSize(Pt(2.4f)).Bold().FontColor(CorMarca).ClampLines(1);
                });

            // Status grande colorido.
            coluna.Item().Height(AlturaStatusMm, Mm).Background(cor).PaddingHorizontal(3.5f, Mm).Row(linha =>
            {
                linha.ConstantItem(9.5f, Mm).AlignMiddle().Width(9.5f, Mm).Height(9.5f, Mm).Svg(IconeSvg(m.Situacao));
                linha.ConstantItem(3, Mm);
                linha.RelativeItem().AlignMiddle().Column(texto =>
                {
                    texto.Item().Text(titulo).FontSize(Pt(7.6f)).ExtraBold().FontColor(Colors.White);
                    texto.Item().PaddingTop(0.6f, Mm).Text(subtitulo.ToUpperInvariant())
                        .FontSize(Pt(2.6f)).Bold().FontColor(Colors.White);
                });
            });

            // Corpo: identificação + dados à esquerda, QR à direita. Altura fixa (o que sobra da etiqueta) e
            // ScaleToFit: texto longo encolhe em vez de empurrar o rodapé para uma segunda página.
            coluna.Item().Height(corpoAltura, Mm).PaddingTop(2.2f, Mm).PaddingHorizontal(3.5f, Mm).ScaleToFit().Row(corpo =>
            {
                corpo.RelativeItem().Column(info =>
                {
                    info.Item().Text(m.ObjetoIdentificacao).FontSize(Pt(5.2f)).ExtraBold().ClampLines(1);
                    if (!string.IsNullOrWhiteSpace(m.ObjetoDescricao))
                        info.Item().Text(m.ObjetoDescricao).FontSize(Pt(2.6f)).SemiBold().FontColor("#444444").ClampLines(1);
                    info.Item().PaddingTop(1.4f, Mm);

                    Linha(info, "Obra", m.ObraNome, destaque: false);
                    Linha(info, "Inspeção", Data(m.InspecaoEm), destaque: false);
                    // "Próxima inspeção até" só aparece se houver periodicidade. No NÃO LIBERADO, sem data, o mockup
                    // mostra que o objeto precisa ser reinspecionado antes de voltar ao uso.
                    if (m.ProximaInspecaoEm is { } proxima)
                        Linha(info, "Próxima até", Data(proxima), destaque: true);
                    else if (naoLiberado)
                        Linha(info, "Próxima", "Reinspeção antes do uso", destaque: true);
                    Linha(info, "Inspetor", m.InspetorNome ?? "—", destaque: false, complemento: m.InspetorFuncao);
                    Linha(info, "Protocolo", m.Protocolo, destaque: false);
                });

                corpo.ConstantItem(24, Mm).Column(qr =>
                {
                    qr.Item().AlignCenter().Width(22, Mm).Height(22, Mm).Border(0.4f, Mm).BorderColor("#111111")
                        .Padding(0.5f, Mm).Image(qrPng).FitArea();
                    qr.Item().PaddingTop(0.8f, Mm).AlignCenter().Text("Confira a situação atual")
                        .FontSize(Pt(2.2f)).Bold().AlignCenter();
                });
            });

            // Rodapé.
            coluna.Item().Height(AlturaRodapeMm, Mm).Background("#f1f0f5").BorderTop(0.3f, Mm).BorderColor("#d8d6e0")
                .PaddingHorizontal(3.5f, Mm).AlignMiddle().Text(t =>
                {
                    t.DefaultTextStyle(e => e.FontSize(Pt(2.1f)).SemiBold().FontColor("#444444"));
                    t.Span("Inspeção interna de SST.").Bold().FontColor(CorMarca);
                    // O aviso da etiqueta de manutenção só faz sentido para extintor.
                    t.Span(EhExtintor(m.Tema)
                        ? " Não substitui etiqueta de manutenção de extintor."
                        : " Leia o QR Code para conferir a situação atual.");
                });
        });
    }

    internal static bool EhExtintor(string tema)
        => tema.Contains("extintor", StringComparison.OrdinalIgnoreCase);

    private const string RecursoLogoAahbrant = "AAHBRANT.SST.Infrastructure.Documentos.Assets.logo-aahbrant.png";
    private static readonly Lazy<byte[]?> LogoAahbrant = new(() =>
    {
        using var stream = typeof(AdesivoInspecaoPdfService).Assembly.GetManifestResourceStream(RecursoLogoAahbrant);
        if (stream is null) return null;
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return memoria.ToArray();
    });

    private static void Linha(ColumnDescriptor coluna, string rotulo, string valor, bool destaque, string? complemento = null)
    {
        coluna.Item().PaddingBottom(0.5f, Mm).Row(linha =>
        {
            linha.ConstantItem(15, Mm).Text(rotulo.ToUpperInvariant()).FontSize(Pt(2.1f)).Bold().FontColor("#777777").ClampLines(1);
            linha.RelativeItem().Column(c =>
            {
                var texto = c.Item().Text(valor).FontSize(Pt(2.8f)).ClampLines(1);
                if (destaque) texto.ExtraBold(); else texto.SemiBold();
                if (!string.IsNullOrWhiteSpace(complemento))
                    c.Item().Text(complemento).FontSize(Pt(2.2f)).FontColor("#555555").ClampLines(1);
            });
        });
    }
}
