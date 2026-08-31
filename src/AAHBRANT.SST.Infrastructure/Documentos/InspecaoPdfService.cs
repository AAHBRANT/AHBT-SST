using AAHBRANT.SST.Application.Inspecoes;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, $"Inspeção — {modelo.TipoInspecao}", modelo.ObraNome, modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(10).Column(coluna =>
                {
                    coluna.Spacing(10);

                    coluna.Item().Element(c => SecaoCabecalho(c, modelo));

                    foreach (var item in modelo.Itens)
                        coluna.Item().Element(c => SecaoItem(c, item));
                });

                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(8);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8);
                });
            });
        });

        return documento.GeneratePdf();
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
                coluna.Item().Height(140).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Image(foto).FitArea();
            else
                coluna.Item().Height(140).Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4)
                    .AlignCenter().AlignMiddle().Text("Sem foto").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
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
