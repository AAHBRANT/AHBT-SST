using AAHBRANT.SST.Application.Treinamentos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Layout alinhado ao modelo AAHBRANT (arquivo de referência "PLANILHA -MODELO RISCOS-FUNÇOES-
// NRS-CERTIFICADOS.XLSX", abas "CERTIFICADO DE NR XX"): frente com os dados do treinamento e
// assinaturas (instrutor + trabalhador), verso com o conteúdo programático do curso. Paleta no
// padrão visual AAHBRANT (#670000/#ebe9ad) no lugar do verde/azul genérico do arquivo de referência.
public class CertificadoTreinamentoPdfService : ICertificadoTreinamentoPdfService
{
    private const string CorMarca = "#670000";
    private const string CorBege = "#ebe9ad";
    private const string CorTexto = "#1a1a1a";

    // Logo padrão da empresa (não confundir com o logo por Obra usado em EntregaEpiPdfService) —
    // embutida como recurso do assembly para não depender de layout de arquivos em runtime/Docker.
    private static readonly byte[] LogoAahbrant = CarregarLogoAahbrant();

    private static byte[] CarregarLogoAahbrant()
    {
        var assembly = typeof(CertificadoTreinamentoPdfService).Assembly;
        using var stream = assembly.GetManifestResourceStream("AAHBRANT.SST.Infrastructure.Documentos.Assets.logo-aahbrant.png")
            ?? throw new InvalidOperationException("Logo padrão da AAHBRANT não encontrada como recurso embutido.");
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return memoria.ToArray();
    }

    public byte[] Gerar(CertificadoTreinamentoPdfModelo modelo)
    {
        var temVerso = !string.IsNullOrWhiteSpace(modelo.ConteudoProgramatico);

        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                ConfigurarPagina(pagina);
                pagina.Content().Padding(14).Column(coluna =>
                {
                    coluna.Spacing(6);
                    coluna.Item().Element(c => Cabecalho(c, modelo, "CERTIFICADO"));
                    coluna.Item().PaddingBottom(4).LineHorizontal(2).LineColor(CorMarca);
                    coluna.Item().Element(c => Frente(c, modelo));
                });
                Rodape(pagina);
            });

            if (temVerso)
            {
                container.Page(pagina =>
                {
                    ConfigurarPagina(pagina);
                    pagina.Content().Padding(14).Column(coluna =>
                    {
                        coluna.Spacing(6);
                        coluna.Item().Element(c => Cabecalho(c, modelo, "CONTEÚDO PROGRAMÁTICO"));
                        coluna.Item().PaddingBottom(4).LineHorizontal(2).LineColor(CorMarca);
                        coluna.Item().Element(c => Verso(c, modelo));
                    });
                    Rodape(pagina);
                });
            }
        });

        return documento.GeneratePdf();
    }

    private static void ConfigurarPagina(PageDescriptor pagina)
    {
        pagina.Size(PageSizes.A4.Landscape());
        pagina.Margin(1.5f, Unit.Centimetre);
        pagina.DefaultTextStyle(estilo => estilo.FontSize(10).FontColor(CorTexto));
        pagina.Background().Border(2).BorderColor(CorMarca);
    }

    private static void Rodape(PageDescriptor pagina)
    {
        pagina.Footer().AlignCenter().Text(t =>
        {
            t.Span("Gerado em ").FontSize(7).FontColor(Colors.Grey.Darken1);
            t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void Cabecalho(IContainer container, CertificadoTreinamentoPdfModelo modelo, string titulo)
    {
        container.Row(linha =>
        {
            linha.ConstantItem(170).Height(45).AlignMiddle().Image(LogoAahbrant).FitArea();

            linha.RelativeItem().AlignCenter().AlignMiddle().Text(titulo).FontSize(20).Bold().FontColor(CorMarca);

            linha.ConstantItem(90).AlignRight().Element(c => Selo(c, modelo.NormaReferencia));
        });
    }

    private static void Selo(IContainer container, string? normaReferencia)
    {
        if (string.IsNullOrWhiteSpace(normaReferencia))
        {
            return;
        }

        container.Background(CorMarca).Padding(6).Column(selo =>
        {
            selo.Item().AlignCenter().Text("NORMA").FontSize(7).FontColor(Colors.White);
            selo.Item().AlignCenter().Text(normaReferencia).FontSize(12).Bold().FontColor(Colors.White);
        });
    }

    private static void Frente(IContainer container, CertificadoTreinamentoPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Spacing(4);

            coluna.Item().PaddingTop(6).Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(11).LineHeight(1.4f));
                t.Span("Certificamos que o(a) Sr(a). ");
                t.Span(modelo.TrabalhadorNome).Bold();
                if (!string.IsNullOrWhiteSpace(modelo.TrabalhadorRg))
                {
                    t.Span($", portador(a) do RG {modelo.TrabalhadorRg},");
                }
                t.Span(" participou do curso de ");
                t.Span(modelo.CursoNome).Bold();
                if (!string.IsNullOrWhiteSpace(modelo.NormaReferencia))
                {
                    t.Span($", em conformidade com a {modelo.NormaReferencia},");
                }
                t.Span($" realizado em {modelo.DataRealizacao:dd/MM/yyyy}, na função de ");
                t.Span(modelo.TrabalhadorFuncaoNome).Bold();
                t.Span(".");
            });

            DescricaoCarga(coluna.Item(), modelo);

            coluna.Item().PaddingTop(4).Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(9));
                t.Span("Promovido pelo SESMT da empresa Aahbrant Engenharia e Construções LTDA");
                if (!string.IsNullOrWhiteSpace(modelo.ObraCnpj))
                {
                    t.Span($", CNPJ {modelo.ObraCnpj}");
                }
                t.Span(".");
            });

            if (!string.IsNullOrWhiteSpace(modelo.ObraEndereco))
            {
                coluna.Item().Text(modelo.ObraEndereco).FontSize(9);
            }

            coluna.Item().PaddingTop(2).Text($"Obra: {modelo.ObraNome}").FontSize(9).SemiBold();

            coluna.Item().PaddingTop(2).Text(
                string.IsNullOrWhiteSpace(modelo.ObraCidade)
                    ? modelo.DataRealizacao.ToString("dd/MM/yyyy")
                    : $"{modelo.ObraCidade}-{modelo.ObraUf}, {modelo.DataRealizacao:dd/MM/yyyy}")
                .FontSize(9).Italic();

            coluna.Item().PaddingTop(6).Background(CorBege).Padding(8).Row(linha =>
            {
                linha.RelativeItem().Text(t =>
                {
                    t.Span("Validade: ").SemiBold();
                    t.Span($"{modelo.DataValidade:dd/MM/yyyy}");
                });
                if (!string.IsNullOrWhiteSpace(modelo.NumeroCertificado))
                {
                    linha.RelativeItem().AlignRight().Text(t =>
                    {
                        t.Span("Nº do certificado: ").SemiBold();
                        t.Span(modelo.NumeroCertificado);
                    });
                }
            });

            coluna.Item().PaddingTop(16).Element(c => Assinaturas(c, modelo));
        });
    }

    private static void Verso(IContainer container, CertificadoTreinamentoPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Spacing(4);

            var topicos = (modelo.ConteudoProgramatico ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            coluna.Item().PaddingTop(4).Column(lista =>
            {
                lista.Spacing(3);
                foreach (var topico in topicos)
                {
                    lista.Item().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(10));
                        t.Span("• ").Bold();
                        t.Span(topico);
                    });
                }
            });

            DescricaoCarga(coluna.Item(), modelo);

            coluna.Item().PaddingTop(20).Element(c => Assinaturas(c, modelo));
        });
    }

    private static void DescricaoCarga(IContainer container, CertificadoTreinamentoPdfModelo modelo)
    {
        container.PaddingTop(6).Text($"Carga horária: {modelo.CargaHorariaRealizada}h realizadas (mínimo exigido: {modelo.CargaHorariaMinima}h).")
            .Bold().FontSize(9);
    }

    private static void Assinaturas(IContainer container, CertificadoTreinamentoPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem().Column(bloco =>
                {
                    bloco.Item().PaddingBottom(2).LineHorizontal(1).LineColor(CorTexto);
                    bloco.Item().AlignCenter().Text(modelo.InstituicaoInstrutor ?? "Instrutor responsável").FontSize(9).SemiBold();
                    bloco.Item().AlignCenter().Text("Instrutor").FontSize(8).Italic();
                });

                linha.ConstantItem(24);

                linha.RelativeItem().Column(bloco =>
                {
                    bloco.Item().PaddingBottom(2).LineHorizontal(1).LineColor(CorTexto);
                    bloco.Item().AlignCenter().Text(modelo.TrabalhadorNome).FontSize(9).SemiBold();
                    bloco.Item().AlignCenter().Text(modelo.TrabalhadorFuncaoNome).FontSize(8).Italic();
                });
            });

            if (modelo.Signatarios.Count == 0)
            {
                coluna.Item().PaddingTop(8).Text("Nenhuma assinatura registrada até o momento.").FontSize(8).Italic().FontColor(Colors.Grey.Darken2);
            }
            else
            {
                coluna.Item().PaddingTop(8).Text("Assinado digitalmente por:").FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2);
                foreach (var signatario in modelo.Signatarios)
                {
                    coluna.Item().Text($"• {signatario.TrabalhadorNome} em {signatario.AssinadoEm:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken2);
                }
            }
        });
    }
}
