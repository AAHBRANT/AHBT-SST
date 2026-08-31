using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Layout padrão de cabeçalho aplicado a todos os documentos gerados/assinados (APR, PT, DDS,
// Ficha de EPI, Relatório de Fiscalização) — decisão do usuário (31/08): a logomarca cadastrada
// na obra passa a identificar visualmente todo documento gerado para ela, em vez de cada PDF ter
// seu próprio cabeçalho. Sem logo (ex.: comprovante de assinatura, que não está preso a uma única
// obra) ou sem obra vinculada, cai no nome fixo "AAHBRANT".
internal static class CabecalhoDocumentoPadrao
{
    private const string CorMarca = "#670000";

    public static void Desenhar(ColumnDescriptor coluna, string tituloDocumento, string? obraNome, byte[]? logoConteudo)
    {
        coluna.Item().Row(linha =>
        {
            if (logoConteudo is not null)
                linha.ConstantItem(50).Height(50).Image(logoConteudo).FitArea();

            linha.RelativeItem().Column(sub =>
            {
                sub.Item().Text(obraNome ?? "AAHBRANT").FontSize(16).Bold().FontColor(CorMarca);
                sub.Item().Text(tituloDocumento).FontSize(12).SemiBold();
            });
        });
        coluna.Item().PaddingTop(4).LineHorizontal(2).LineColor(CorMarca);
    }
}
