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
        // Logo à esquerda e um espaço reservado do mesmo tamanho à direita (mesmo sem logo) — assim
        // o bloco de título fica centralizado de verdade em relação à página inteira, não só ao
        // espaço sobrando depois da logo (pedido do usuário, 04/09: título centralizado em todos os
        // documentos).
        coluna.Item().Row(linha =>
        {
            linha.ConstantItem(50).Height(50).Element(c =>
            {
                if (logoConteudo is not null) c.Image(logoConteudo).FitArea();
            });

            linha.RelativeItem().AlignCenter().Column(sub =>
            {
                sub.Item().AlignCenter().Text(obraNome ?? "Obra não identificada").FontSize(16).Bold().FontColor(CorMarca);
                sub.Item().AlignCenter().Text(tituloDocumento).FontSize(12).SemiBold();
            });

            linha.ConstantItem(50);
        });
        coluna.Item().PaddingTop(4).LineHorizontal(2).LineColor(CorMarca);
    }
}
