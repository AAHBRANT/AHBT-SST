using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Layout padrão de cabeçalho aplicado à maioria dos documentos gerados/assinados (APR, PT, DDS,
// Ficha de EPI, CIPA, Relatório de Fiscalização, comprovante de assinatura, Ata de Treinamento).
//
// A ÚNICA logo permitida em qualquer documento é a cadastrada na própria Obra (pedido do usuário,
// 04/09: "a única logo a ser usada nos documentos deve ser a cadastrada na obra... remova a logo da
// AAHBRANT de todos documentos pois não tá cadastrado nessa obra do Cuiá") — revoga a decisão
// anterior (01/09) que fixava a logomarca da AAHBRANT sempre à esquerda. Sem logo cadastrada na
// Obra, o slot fica em branco (mesmo princípio já usado em InspecaoPdfService.CabecalhoInspecao,
// que não passa por este componente).
internal static class CabecalhoDocumentoPadrao
{
    private const string CorMarca = "#670000";

    // Largura do bloco lateral (logo à esquerda / nº do documento à direita) — as duas laterais usam
    // a mesma largura pra o bloco de título ficar centralizado de verdade em relação à página inteira
    // (pedido do usuário, 04/09), mesmo com texto real ocupando o lado direito agora.
    private const float LarguraLateral = 200;

    public static void Desenhar(ColumnDescriptor coluna, string tituloDocumento, string? obraNome, byte[]? logoConteudo,
        IReadOnlyList<string>? linhasCabecalhoDireita = null, IReadOnlyList<string>? linhasCabecalhoEsquerda = null)
    {
        coluna.Item().Row(linha =>
        {
            linha.ConstantItem(LarguraLateral).Row(ladoEsquerdo =>
            {
                ladoEsquerdo.ConstantItem(50).Height(50).Element(c =>
                {
                    if (logoConteudo is not null) c.Image(logoConteudo).FitArea();
                });

                if (linhasCabecalhoEsquerda is { Count: > 0 })
                {
                    ladoEsquerdo.RelativeItem().PaddingLeft(4).Column(esquerda =>
                    {
                        foreach (var texto in linhasCabecalhoEsquerda)
                        {
                            esquerda.Item().Text(t =>
                            {
                                t.Justify();
                                t.Span(texto).FontSize(8).SemiBold();
                            });
                        }
                    });
                }
                else
                {
                    ladoEsquerdo.ConstantItem(LarguraLateral - 50);
                }
            });

            linha.RelativeItem().AlignCenter().Column(sub =>
            {
                sub.Item().AlignCenter().Text(obraNome ?? "Obra não identificada").FontSize(16).Bold().FontColor(CorMarca);
                sub.Item().AlignCenter().Text(tituloDocumento).FontSize(12).SemiBold();
            });

            // Metadados de identificação do documento (nº, data, local, referência) sobem pro
            // cabeçalho, lado direito, empilhados e justificados (pedido do usuário, 04/09 — saíam do
            // corpo do documento, misturados com OBRA/CONTRATO, ATIVIDADE e MÁQUINAS/EQUIP.). Sem
            // metadados (documentos que não usam este recurso), o slot fica em branco, mesmo princípio
            // do slot da logo.
            if (linhasCabecalhoDireita is { Count: > 0 })
            {
                linha.ConstantItem(LarguraLateral).Column(direita =>
                {
                    foreach (var texto in linhasCabecalhoDireita)
                    {
                        direita.Item().AlignRight().Text(t =>
                        {
                            t.Justify();
                            t.Span(texto).FontSize(8).SemiBold();
                        });
                    }
                });
            }
            else
            {
                linha.ConstantItem(LarguraLateral);
            }
        });
        coluna.Item().PaddingTop(4).LineHorizontal(2).LineColor(CorMarca);
    }
}
