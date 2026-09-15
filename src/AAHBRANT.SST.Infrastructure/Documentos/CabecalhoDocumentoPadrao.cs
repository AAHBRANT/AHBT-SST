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

    // Largura do bloco lateral quando ele carrega metadados de verdade (nº do documento, data,
    // local...) — só a APR usa isso hoje. Documentos sem metadados usam LarguraLateralSemMetadados
    // (só a largura da logo) pra não roubar espaço do título à toa — reservar 200pt fixos dos dois
    // lados mesmo vazio deixava o título espremido num miolo de ~127pt e qualquer nome de obra
    // realista quebrava em 2-3 linhas (achado revisando os documentos, 10/09).
    private const float LarguraLateral = 200;
    private const float LarguraLateralSemMetadados = 50;

    public static void Desenhar(ColumnDescriptor coluna, string tituloDocumento, string? obraNome, byte[]? logoConteudo,
        IReadOnlyList<string>? linhasCabecalhoDireita = null, IReadOnlyList<string>? linhasCabecalhoEsquerda = null)
    {
        var larguraEsquerda = linhasCabecalhoEsquerda is { Count: > 0 } ? LarguraLateral : LarguraLateralSemMetadados;
        var larguraDireita = linhasCabecalhoDireita is { Count: > 0 } ? LarguraLateral : LarguraLateralSemMetadados;
        // Com metadados dos dois lados o miolo central fica bem mais estreito (~127pt contra ~427pt
        // sem eles) — usa a tabela de fonte mais agressiva pra esse caso (só a APR hoje).
        var colunaEstreita = linhasCabecalhoEsquerda is { Count: > 0 } || linhasCabecalhoDireita is { Count: > 0 };
        var nomeObra = obraNome ?? "Obra não identificada";

        coluna.Item().Row(linha =>
        {
            linha.ConstantItem(larguraEsquerda).Row(ladoEsquerdo =>
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
                                t.Span(texto).FontSize(8).SemiBold();
                            });
                        }
                    });
                }
            });

            linha.RelativeItem().AlignCenter().Column(sub =>
            {
                sub.Item().AlignCenter().Text(nomeObra).FontSize(FonteNomeObra(nomeObra, colunaEstreita)).Bold().FontColor(CorMarca);
                sub.Item().AlignCenter().Text(tituloDocumento).FontSize(12).SemiBold();
            });

            // Metadados de identificação do documento (nº, data, local, referência) sobem pro
            // cabeçalho, lado direito, empilhados e justificados (pedido do usuário, 04/09 — saíam do
            // corpo do documento, misturados com OBRA/CONTRATO, ATIVIDADE e MÁQUINAS/EQUIP.). Sem
            // metadados (documentos que não usam este recurso), o slot vira só um espaçador estreito,
            // simétrico à logo do lado esquerdo.
            if (linhasCabecalhoDireita is { Count: > 0 })
            {
                linha.ConstantItem(larguraDireita).Column(direita =>
                {
                    foreach (var texto in linhasCabecalhoDireita)
                    {
                        direita.Item().AlignRight().Text(t =>
                        {
                            t.Span(texto).FontSize(8).SemiBold();
                        });
                    }
                });
            }
            else
            {
                linha.ConstantItem(larguraDireita);
            }
        });
        coluna.Item().PaddingTop(4).LineHorizontal(2).LineColor(CorMarca);
    }

    // Nomes de obra/contrato mais longos (ex.: "CONSÓRCIO PONTE RIO CUIÁ" ou razões sociais
    // completas) quebravam em várias linhas — reduz a fonte em vez de deixar quebrar. Limiares
    // escolhidos visualmente (renderizando e conferindo o PDF, não uma fórmula de largura de fonte).
    // A coluna estreita (com metadados dos dois lados, ver `colunaEstreita` acima) precisa de
    // limiares bem mais agressivos pro mesmo texto não estourar: até "CONSÓRCIO PONTE RIO CUIÁ" (24
    // caracteres) já quebrava e quase encostava no bloco da direita a 10pt — só ficou limpo a 8pt.
    internal static float FonteNomeObra(string nomeObra, bool colunaEstreita = false) => colunaEstreita
        ? nomeObra.Length switch
        {
            <= 15 => 11,
            _ => 8,
        }
        : nomeObra.Length switch
        {
            <= 35 => 16,
            <= 55 => 13,
            _ => 11,
        };
}
