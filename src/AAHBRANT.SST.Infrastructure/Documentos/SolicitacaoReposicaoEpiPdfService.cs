using AAHBRANT.SST.Application.EstoquesEpi.Reposicao;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Solicitação de Reposição de EPI (especificação 21/10/2026, Módulo 2): pedido interno para Suprimentos
// com o que está abaixo do mínimo na obra, assinado pelo técnico. Mesmo cabeçalho e rodapé de
// rastreabilidade dos demais documentos do sistema.
public class SolicitacaoReposicaoEpiPdfService : ISolicitacaoReposicaoEpiPdfService
{
    private const string CorMarca = "#670000";
    private const string Titulo = "Solicitação de Reposição de EPI";

    public byte[] Gerar(SolicitacaoReposicaoEpiPdfModelo modelo)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(1.6f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(9.5f));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, Titulo, modelo.ObraNome, modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(10).Column(coluna =>
                {
                    coluna.Spacing(8);

                    coluna.Item().Row(linha =>
                    {
                        linha.RelativeItem().Text(t =>
                        {
                            t.Span("Nº do documento: ").SemiBold();
                            t.Span(modelo.NumeroDocumento);
                        });
                        linha.RelativeItem().AlignRight().Text(t =>
                        {
                            t.Span("Emitida em: ").SemiBold();
                            t.Span(HorarioBrasilia.De(modelo.EmitidaEmUtc).ToString("dd/MM/yyyy HH:mm"));
                        });
                    });
                    coluna.Item().Text(t =>
                    {
                        t.Span("Prazo de reposição considerado: ").SemiBold();
                        t.Span($"{modelo.PrazoReposicaoDias} dias");
                    });

                    coluna.Item().PaddingTop(4).Text(
                        "Itens com disponível abaixo do estoque mínimo da obra. Disponível = saldo físico menos as unidades já separadas " +
                        "para kits de funcionários novos. A quantidade sugerida recompõe o estoque até o dobro do mínimo.")
                        .FontSize(8.5f).FontColor(Colors.Grey.Darken2);

                    coluna.Item().Table(tabela =>
                    {
                        tabela.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(2);
                        });

                        tabela.Header(cabecalho =>
                        {
                            foreach (var titulo in new[] { "EPI", "CA", "Saldo", "Separado", "Mínimo", "Repor", "Tamanhos em falta" })
                                cabecalho.Cell().Background(CorMarca).Padding(4).Text(titulo).FontColor(Colors.White).SemiBold().FontSize(8.5f);
                        });

                        var zebra = false;
                        foreach (var item in modelo.Itens)
                        {
                            var fundo = zebra ? Colors.Grey.Lighten4 : Colors.White;
                            zebra = !zebra;
                            Celula(tabela, fundo).Text(item.EpiNome);
                            Celula(tabela, fundo).Text(item.CertificadoAprovacaoNumero ?? "—");
                            Celula(tabela, fundo).AlignRight().Text(item.Saldo.ToString());
                            Celula(tabela, fundo).AlignRight().Text(item.Reservado.ToString());
                            Celula(tabela, fundo).AlignRight().Text(item.EstoqueMinimo.ToString());
                            Celula(tabela, fundo).AlignRight().Text(item.QuantidadeSugerida.ToString()).SemiBold();
                            Celula(tabela, fundo).Text(item.TamanhosEmFalta ?? "—");
                        }
                    });

                    coluna.Item().AlignRight().Text(t =>
                    {
                        t.Span("Total a repor: ").SemiBold();
                        t.Span($"{modelo.Itens.Sum(i => i.QuantidadeSugerida)} unidade(s) em {modelo.Itens.Count} item(ns)");
                    });

                    if (!string.IsNullOrWhiteSpace(modelo.Observacao))
                        coluna.Item().Text(t =>
                        {
                            t.Span("Observação: ").SemiBold();
                            t.Span(modelo.Observacao);
                        });

                    coluna.Item().PaddingTop(24).AlignCenter().Width(260).Element(e =>
                        AssinaturaPdfPadrao.Bloco(e, "Solicitante", modelo.SolicitanteNome, modelo.SolicitanteFuncao,
                            modelo.AssinadoEmUtc, modelo.MetodoAssinatura));
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, Titulo, modelo.NumeroDocumento, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng,
                    temAssinatura: modelo.AssinadoEmUtc is not null));
            });
        });

        return documento.GeneratePdf();
    }

    private static IContainer Celula(TableDescriptor tabela, string fundo) =>
        tabela.Cell().Background(fundo).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(4);
}
