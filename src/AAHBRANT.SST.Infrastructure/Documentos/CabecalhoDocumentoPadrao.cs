using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Layout padrão de cabeçalho aplicado à maioria dos documentos gerados/assinados (APR, PT, DDS,
// Ficha de EPI, CIPA, Relatório de Fiscalização, comprovante de assinatura) — decisão do usuário
// (01/09): a logomarca da AAHBRANT ocupa sempre o slot fixo à esquerda, em todo documento gerado
// pelo sistema (mesmo recurso embutido já usado em CertificadoTreinamentoPdfService, único tipo de
// documento com ressalva — já tem cabeçalho e logo próprios, não passa por aqui). A logomarca da
// obra (quando cadastrada) continua aparecendo, agora como selo secundário à direita, para manter a
// identificação visual de qual canteiro gerou o documento.
//
// EXCEÇÃO — Inspeção/Patrulha de Segurança: não usa este componente (decisão do usuário, 01/09,
// escopo restrito só a esse documento por enquanto). Lá o slot de logo fica sempre em branco — ver
// InspecaoPdfService.CabecalhoInspecao. LogoAahbrant segue internal (não private) por precaução,
// caso outro documento futuro precise reaproveitar o mesmo recurso embutido sem duplicar o
// carregamento — hoje nenhum outro consumidor além deste arquivo usa essa visibilidade.
internal static class CabecalhoDocumentoPadrao
{
    private const string CorMarca = "#670000";

    internal static readonly byte[] LogoAahbrant = CarregarLogoAahbrant();

    private static byte[] CarregarLogoAahbrant()
    {
        var assembly = typeof(CabecalhoDocumentoPadrao).Assembly;
        using var stream = assembly.GetManifestResourceStream("AAHBRANT.SST.Infrastructure.Documentos.Assets.logo-aahbrant.png")
            ?? throw new InvalidOperationException("Logo padrão da AAHBRANT não encontrada como recurso embutido.");
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return memoria.ToArray();
    }

    public static void Desenhar(ColumnDescriptor coluna, string tituloDocumento, string? obraNome, byte[]? logoConteudo)
    {
        coluna.Item().Row(linha =>
        {
            linha.ConstantItem(50).Height(50).Image(LogoAahbrant).FitArea();

            linha.RelativeItem().PaddingLeft(8).Column(sub =>
            {
                sub.Item().Text(obraNome ?? "AAHBRANT").FontSize(16).Bold().FontColor(CorMarca);
                sub.Item().Text(tituloDocumento).FontSize(12).SemiBold();
            });

            if (logoConteudo is not null)
                linha.ConstantItem(50).Height(50).PaddingLeft(8).Image(logoConteudo).FitArea();
        });
        coluna.Item().PaddingTop(4).LineHorizontal(2).LineColor(CorMarca);
    }
}
