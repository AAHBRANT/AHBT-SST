using AAHBRANT.SST.Application.Inspecoes;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Um bloco por achado (Nº, descrição, local, responsável, prazo, status, plano de ação, evidência
// anterior/posterior lado a lado) — mesma informação da planilha "Patrulha de Segurança do
// Trabalho" do usuário (31/08), mas no layout padrão de documentos do sistema.
public class InspecaoPdfService : IInspecaoPdfService
{
    private const string CorMarca = "#670000";
    private const string CorResolvido = "#00B050";
    private const string CorPendente = "#FFC000";
    private const string CorNaoAplicavel = "#D9D9D9";

    public byte[] Gerar(InspecaoPdfModelo modelo)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(1.5f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(9));

                pagina.Header().Column(coluna => CabecalhoInspecao(coluna, $"Inspeção — {modelo.TipoInspecao}", modelo));

                pagina.Content().PaddingVertical(10).Column(coluna =>
                {
                    coluna.Spacing(10);

                    coluna.Item().Element(c => SecaoCabecalho(c, modelo));

                    foreach (var item in modelo.Itens)
                        coluna.Item().Element(c => SecaoItem(c, item));
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Inspeção", modelo.Protocolo, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
            });
        });

        return documento.GeneratePdf();
    }

    // Cabeçalho próprio da Inspeção/Patrulha de Segurança — não usa CabecalhoDocumentoPadrao (que
    // fixa a logo da AAHBRANT pros demais documentos). Decisão do usuário (01/09): o slot de logo
    // fica sempre em branco neste documento (assunto resolvido — sem lógica condicional por tipo de
    // execução da obra).
    private static void CabecalhoInspecao(ColumnDescriptor coluna, string tituloDocumento, InspecaoPdfModelo modelo)
    {
        coluna.Item().Row(linha =>
        {
            linha.RelativeItem().Column(sub =>
            {
                sub.Item().Text(modelo.ObraNome ?? "Inspeção").FontSize(16).Bold().FontColor(CorMarca);
                sub.Item().Text(tituloDocumento).FontSize(12).SemiBold();
            });
        });
        coluna.Item().PaddingTop(4).LineHorizontal(2).LineColor(CorMarca);
    }

    private static void SecaoCabecalho(IContainer container, InspecaoPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem().Text(t => { t.Span("Checklist: ").SemiBold(); t.Span($"{modelo.ChecklistNome} (v{modelo.ChecklistVersao})"); });
                linha.RelativeItem().Text(t => { t.Span("Data: ").SemiBold(); t.Span(modelo.Data.ToString("dd/MM/yyyy")); });
            });
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem().Text(t => { t.Span("Responsável: ").SemiBold(); t.Span(modelo.ResponsavelNome); });
                linha.RelativeItem().Text(t => { t.Span("Status: ").SemiBold(); t.Span(modelo.Status); });
            });
        });
    }

    private static void SecaoItem(IContainer container, InspecaoPdfItemModelo item)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(coluna =>
        {
            coluna.Spacing(4);

            coluna.Item().Row(linha =>
            {
                linha.RelativeItem().Text($"{item.Ordem}. {item.Descricao}").FontSize(10).Bold().FontColor(CorMarca);
                linha.ConstantItem(90).Element(CelulaStatus(item.StatusItem)).AlignCenter()
                    .Text(RotuloStatus(item.StatusItem)).FontSize(8).Bold();
            });

            coluna.Item().Row(linha =>
            {
                linha.RelativeItem().Text(t => { t.Span("Local: ").SemiBold(); t.Span(item.Local ?? "não informado"); });
                linha.RelativeItem().Text(t => { t.Span("Responsável: ").SemiBold(); t.Span(item.ResponsavelNome ?? "não informado"); });
                linha.RelativeItem().Text(t => { t.Span("Prazo: ").SemiBold(); t.Span(item.Prazo.HasValue ? item.Prazo.Value.ToString("dd/MM/yyyy") : "não informado"); });
            });

            if (!string.IsNullOrWhiteSpace(item.Observacao))
                coluna.Item().Text(t => { t.Span("OBS: ").SemiBold(); t.Span(item.Observacao); });

            if (!string.IsNullOrWhiteSpace(item.PlanoDeAcao))
                coluna.Item().Text(t => { t.Span("Plano de ação: ").SemiBold(); t.Span(item.PlanoDeAcao); });

            coluna.Item().PaddingTop(4).Row(linha =>
            {
                linha.RelativeItem().Element(c => BlocoEvidencia(c, "Evidência anterior", item.FotoAntesConteudo));
                linha.ConstantItem(8);
                linha.RelativeItem().Element(c => BlocoEvidencia(c, "Evidência posterior", item.FotoDepoisConteudo));
            });
        });
    }

    private static void BlocoEvidencia(IContainer container, string titulo, byte[]? foto)
    {
        container.Column(coluna =>
        {
            coluna.Item().Text(titulo).FontSize(8).SemiBold();
            if (foto is not null)
                coluna.Item().Height(140).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Image(RecortarParaPreencherSlot(foto)).FitArea();
            else
                coluna.Item().Height(140).Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4)
                    .AlignCenter().AlignMiddle().Text("Sem foto").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    // Proporção aproximada do slot de evidência (largura da coluna ÷ Height(140) acima) — fotos de
    // celular vêm em proporções variadas e o antigo Image(foto).FitArea() só "contém" a imagem
    // (letterbox), deixando faixas vazias quando a proporção não bate com a do slot. Recorta pelo
    // centro na proporção do slot ANTES de desenhar, pra imagem preencher o quadro inteiro (mesmo
    // princípio de object-fit: cover), sem depender de um modo "cover" nativo do QuestPDF (não existe
    // nesta versão — só FitWidth/FitHeight/FitArea/FitUnproportionally). Usa SixLabors.ImageSharp
    // (100% gerenciado, sem binário nativo) em vez de SkiaSharp puro — QuestPDF não expõe a
    // SkiaSharp pública pro net8.0 (embute seu próprio wrapper interno), e adicionar SkiaSharp como
    // dependência própria puxaria pacote de binário nativo por RID (Linux no container do Azure),
    // risco maior do que o necessário só pra recortar uma foto.
    private static byte[] RecortarParaPreencherSlot(byte[] foto)
    {
        using var imagem = SixLabors.ImageSharp.Image.Load(foto);
        imagem.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(800, 450), // ~16:9, próximo da proporção real do slot
            Mode = ResizeMode.Crop,
        }));

        using var saida = new MemoryStream();
        imagem.Save(saida, new JpegEncoder { Quality = 85 });
        return saida.ToArray();
    }

    private static Func<IContainer, IContainer> CelulaStatus(StatusItemChecklist? status)
    {
        var cor = status switch
        {
            StatusItemChecklist.Conforme => CorResolvido,
            StatusItemChecklist.NaoConforme => CorPendente,
            StatusItemChecklist.NaoAplicavel => CorNaoAplicavel,
            _ => CorNaoAplicavel,
        };
        return container => container.Background(cor).Padding(4).CornerRadius(4);
    }

    private static string RotuloStatus(StatusItemChecklist? status) => status switch
    {
        StatusItemChecklist.Conforme => "RESOLVIDO",
        StatusItemChecklist.NaoConforme => "PENDENTE",
        StatusItemChecklist.NaoAplicavel => "NÃO APLICÁVEL",
        _ => "NÃO RESPONDIDO",
    };
}
