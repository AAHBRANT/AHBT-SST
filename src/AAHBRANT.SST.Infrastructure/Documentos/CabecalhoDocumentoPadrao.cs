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

    public static void Desenhar(ColumnDescriptor coluna, string tituloDocumento, string? obraNome, byte[]? logoConteudo)
    {
        coluna.Item().Row(linha =>
        {
            if (logoConteudo is not null)
                linha.ConstantItem(50).Height(50).Image(logoConteudo).FitArea();

            linha.RelativeItem().PaddingLeft(logoConteudo is not null ? 8 : 0).Column(sub =>
            {
                sub.Item().Text(obraNome ?? "Obra não identificada").FontSize(16).Bold().FontColor(CorMarca);
                sub.Item().Text(tituloDocumento).FontSize(12).SemiBold();
            });
        });
        coluna.Item().PaddingTop(4).LineHorizontal(2).LineColor(CorMarca);
    }
}
