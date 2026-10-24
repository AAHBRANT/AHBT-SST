using AAHBRANT.SST.Application.Acidentes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Relatório da ocorrência. A seção de lesão traz o mapa corporal (MapaCorporalSvg) com as partes
// atingidas marcadas e a lista por extenso ao lado — o mapa sozinho não basta para quem lê impresso.
public class AcidentePdfService : IAcidentePdfService
{
    private const string CorMarca = "#670000";
    private const string CorAlerta = "#C4262E";

    public byte[] Gerar(AcidentePdfModelo modelo)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(1.5f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(9));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, $"Registro de Ocorrência — {modelo.Tipo}", modelo.ObraNome, modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(10).Column(coluna =>
                {
                    coluna.Spacing(12);
                    coluna.Item().Element(c => SecaoIdentificacao(c, modelo));
                    coluna.Item().Element(c => Secao(c, "Descrição da ocorrência", s => s.Item().Text(modelo.Descricao)));
                    if (TemLesao(modelo))
                        coluna.Item().Element(c => SecaoLesao(c, modelo));
                    coluna.Item().Element(c => SecaoAfastamento(c, modelo));
                    if (modelo.Metodologia is not null || !string.IsNullOrWhiteSpace(modelo.Causas))
                        coluna.Item().Element(c => Secao(c, "Investigação (análise de causas)", s =>
                        {
                            if (modelo.Metodologia is not null) s.Item().Element(c2 => Campo(c2, "Metodologia", modelo.Metodologia));
                            if (!string.IsNullOrWhiteSpace(modelo.Causas)) s.Item().Element(c2 => Campo(c2, "Causas", modelo.Causas));
                        }));
                    if (modelo.Acoes.Count > 0)
                        coluna.Item().Element(c => SecaoPlanoAcao(c, modelo.Acoes));
                    if (modelo.Fotos.Count > 0)
                        coluna.Item().Element(c => SecaoFotos(c, modelo.Fotos));
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Registro de ocorrência", null, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
            });
        });

        return documento.GeneratePdf();
    }

    // Lesão é do acidente e da doença ocupacional; nos demais tipos a seção só sai se algo foi informado.
    private static bool TemLesao(AcidentePdfModelo m) =>
        m.Tipo is "Acidente" or "Doença ocupacional" || !string.IsNullOrWhiteSpace(m.Lesao) || m.RegioesCorpo.Count > 0;

    private static void Secao(IContainer container, string titulo, Action<ColumnDescriptor> conteudo)
    {
        container.Column(coluna =>
        {
            coluna.Spacing(5);
            coluna.Item().BorderBottom(1).BorderColor(CorMarca).PaddingBottom(2).Text(titulo.ToUpperInvariant()).FontSize(10).Bold().FontColor(CorMarca);
            conteudo(coluna);
        });
    }

    private static void Campo(IContainer container, string rotulo, string? valor) =>
        container.Text(t =>
        {
            t.Span($"{rotulo}: ").SemiBold();
            t.Span(string.IsNullOrWhiteSpace(valor) ? "não informado" : valor);
        });

    private static void SecaoIdentificacao(IContainer container, AcidentePdfModelo m) => Secao(container, "Identificação", s =>
    {
        s.Item().Row(linha =>
        {
            linha.RelativeItem().Element(c => Campo(c, "Tipo", m.Tipo));
            linha.RelativeItem().Element(c => Campo(c, "Situação", m.Status));
        });
        s.Item().Row(linha =>
        {
            var hora = m.Hora is { } h ? $" às {h:hh\\:mm}" : string.Empty;
            linha.RelativeItem().Element(c => Campo(c, "Data", $"{m.Data:dd/MM/yyyy}{hora}"));
            linha.RelativeItem().Element(c => Campo(c, "Local", m.Local));
        });
        s.Item().Element(c => Campo(c, "Atividade", m.AtividadeNome));
        s.Item().Element(c => Campo(c, m.Envolvidos.Count > 1 ? "Envolvidos" : "Envolvido", m.Envolvidos.Count > 0 ? string.Join(", ", m.Envolvidos) : null));
    });

    private static void SecaoLesao(IContainer container, AcidentePdfModelo m) => Secao(container, "Lesão e partes do corpo atingidas", s =>
    {
        s.Item().Element(c => Campo(c, "Lesão", m.Lesao));
        s.Item().ShowEntire().Row(linha =>
        {
            foreach (var vista in MapaCorporalSvg.Desenhar(m.RegioesCorpo))
            {
                linha.ConstantItem(118).Column(figura =>
                {
                    figura.Item().AlignCenter().Text(vista.Titulo.ToUpperInvariant()).FontSize(6.5f).SemiBold().FontColor(Colors.Grey.Darken1);
                    figura.Item().PaddingTop(2).Height(300).AlignCenter().Svg(vista.Svg).FitArea();
                    // Lado do trabalhador: na frente, o direito fica à esquerda de quem lê.
                    figura.Item().Row(lados =>
                    {
                        lados.RelativeItem().AlignLeft().PaddingLeft(14).Text(vista.Id == "F" ? "D" : "E").FontSize(7).Bold().FontColor(Colors.Grey.Darken1);
                        lados.RelativeItem().AlignRight().PaddingRight(14).Text(vista.Id == "F" ? "E" : "D").FontSize(7).Bold().FontColor(Colors.Grey.Darken1);
                    });
                });
                linha.ConstantItem(6);
            }

            linha.RelativeItem().PaddingLeft(6).Column(lista =>
            {
                lista.Spacing(3);
                lista.Item().Text("Partes atingidas").SemiBold();
                if (m.RegioesCorpo.Count == 0)
                    lista.Item().Text("Nenhuma parte do corpo marcada no mapa.").FontColor(Colors.Grey.Darken1);
                foreach (var codigo in MapaCorporalSvg.Ordenar(m.RegioesCorpo))
                    lista.Item().Row(item =>
                    {
                        item.ConstantItem(8).PaddingTop(3).Height(5).Width(5).Background(CorAlerta).CornerRadius(1);
                        item.RelativeItem().Text(MapaCorporalSvg.Rotulo(codigo));
                    });
            });
        });
        s.Item().Element(c => Campo(c, "Consequência", m.Consequencia));
        s.Item().Element(c => Campo(c, "Atendimento", m.Atendimento));
    });

    private static void SecaoAfastamento(IContainer container, AcidentePdfModelo m) => Secao(container, "Afastamento e gravidade (NBR 14280)", s =>
    {
        s.Item().Row(linha =>
        {
            linha.RelativeItem().Element(c => Campo(c, "Houve afastamento",
                m.HouveAfastamento ? (m.DiasAfastamento is { } d ? $"sim, {d} dia(s)" : "sim") : "não"));
            linha.RelativeItem().Element(c => Campo(c, "Gravidade", m.Gravidade));
        });
        s.Item().Row(linha =>
        {
            linha.RelativeItem().Element(c => Campo(c, "Dias debitados", m.DiasDebitados.ToString()));
            linha.RelativeItem().Element(c => Campo(c, "Nº da CAT", m.NumeroCat));
        });
    });

    private static void SecaoPlanoAcao(IContainer container, IReadOnlyList<AcidentePdfAcaoModelo> acoes) => Secao(container, "Plano de ação", s =>
    {
        s.Item().Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.ConstantColumn(62);
                colunas.RelativeColumn();
                colunas.ConstantColumn(48);
                colunas.ConstantColumn(52);
                colunas.ConstantColumn(90);
                colunas.ConstantColumn(58);
            });
            tabela.Header(cabecalho =>
            {
                foreach (var titulo in new[] { "Tipo", "Ação", "Prioridade", "Prazo", "Responsável", "Situação" })
                    cabecalho.Cell().Background(CorMarca).Padding(3).Text(titulo).FontSize(8).Bold().FontColor(Colors.White);
            });
            foreach (var acao in acoes)
            {
                foreach (var valor in new[]
                {
                    acao.Tipo, acao.Descricao, acao.Prioridade, acao.Prazo?.ToString("dd/MM/yyyy") ?? "—",
                    acao.ResponsavelNome ?? "—", acao.Status,
                })
                    tabela.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(valor).FontSize(8);
            }
        });
    });

    private static void SecaoFotos(IContainer container, IReadOnlyList<byte[]> fotos) => Secao(container, "Fotos do local", s =>
    {
        s.Item().Row(linha =>
        {
            linha.Spacing(6);
            foreach (var foto in fotos.Take(3))
            {
                var imagem = Normalizar(foto);
                linha.RelativeItem().Height(130).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Element(c =>
                {
                    if (imagem is not null) c.Image(imagem).FitArea();
                    else c.AlignCenter().AlignMiddle().Text("Foto indisponível").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            }
        });
    });

    // Foto de celular pode vir enorme ou em formato que o QuestPDF não lê: reduz e regrava em JPEG.
    private static byte[]? Normalizar(byte[] foto)
    {
        try
        {
            using var imagem = AAHBRANT.SST.Infrastructure.Imagens.ImagemSegura.Carregar(foto);
            imagem.Mutate(x => x.Resize(new ResizeOptions { Size = new SixLabors.ImageSharp.Size(1200, 1200), Mode = ResizeMode.Max }));
            using var saida = new MemoryStream();
            imagem.Save(saida, new JpegEncoder { Quality = 82 });
            return saida.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
