using AAHBRANT.SST.Application.Trabalhadores;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Imagens;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AAHBRANT.SST.Infrastructure.Trabalhadores;

// Índice e páginas de anexo em imagem do Dossiê do Funcionário (ver GerarDossieTrabalhadorQuery).
// Mesmo cabeçalho e cor de marca do Relatório de Fiscalização, que vem logo antes no mesmo PDF.
public class DossieTrabalhadorPdfService : IDossieTrabalhadorPdfService
{
    private const string CorMarca = "#670000";

    public byte[] GerarIndice(string trabalhadorNome, string obraNome, byte[]? obraLogo, IReadOnlyList<ItemIndiceDossie> itens)
    {
        return Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(10));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "Dossiê do Funcionário — Índice dos documentos", obraNome, obraLogo));

                pagina.Content().PaddingVertical(12).Column(coluna =>
                {
                    coluna.Spacing(4);
                    coluna.Item().Text(trabalhadorNome).FontSize(13).Bold().FontColor(CorMarca);
                    coluna.Item().Text("Os documentos abaixo seguem, na íntegra e nesta ordem, logo após este índice. " +
                                       "Cada um mantém o próprio QR Code de validação.").FontSize(9).Italic();

                    if (itens.Count == 0)
                    {
                        coluna.Item().PaddingTop(8).Text("Nenhum documento além do relatório de fiscalização.").Italic();
                        return;
                    }

                    var numero = 0;
                    foreach (var secao in itens.GroupBy(i => i.Secao))
                    {
                        coluna.Item().PaddingTop(8).EnsureSpace(40).Text(secao.Key).FontSize(11).Bold().FontColor(CorMarca);
                        coluna.Item().Table(tabela =>
                        {
                            tabela.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(24);
                                c.RelativeColumn();
                                c.ConstantColumn(62);
                            });
                            foreach (var item in secao)
                            {
                                var rotulo = item.Incluido ? (++numero).ToString() : "—";
                                Celula(tabela).Text(rotulo).FontSize(9).FontColor(Colors.Grey.Darken1);
                                Celula(tabela).Column(c =>
                                {
                                    c.Item().Text(item.Titulo).FontSize(9.5f);
                                    if (!item.Incluido)
                                        c.Item().Text("Não incluído: " + item.Observacao).FontSize(8).Italic().FontColor(Colors.Grey.Darken2);
                                });
                                Celula(tabela).AlignRight().Text(item.Data is { } d ? HorarioBrasilia.De(d).ToString("dd/MM/yyyy") : "").FontSize(9);
                            }
                        });
                    }
                });

                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(9);
                    t.Span(HorarioBrasilia.Agora.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                });
            });
        }).GeneratePdf();
    }

    public byte[]? ImagemComoPdf(string titulo, byte[] imagem, string obraNome, byte[]? obraLogo)
    {
        byte[] normalizada;
        try
        {
            // Endireita foto de celular (EXIF) e reduz para não inchar o PDF.
            using var img = ImagemSegura.Carregar(imagem);
            img.Mutate(x => x.AutoOrient());
            if (Math.Max(img.Width, img.Height) > 2000)
                img.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new SixLabors.ImageSharp.Size(2000, 2000) }));
            using var saida = new MemoryStream();
            img.Save(saida, new JpegEncoder { Quality = 85 });
            normalizada = saida.ToArray();
        }
        catch
        {
            return null;
        }

        return Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.Header().Column(coluna => CabecalhoDocumentoPadrao.Desenhar(coluna, titulo, obraNome, obraLogo));
                pagina.Content().PaddingVertical(12).AlignCenter().Image(normalizada).FitArea();
            });
        }).GeneratePdf();
    }

    private static IContainer Celula(TableDescriptor tabela) =>
        tabela.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(2);
}
