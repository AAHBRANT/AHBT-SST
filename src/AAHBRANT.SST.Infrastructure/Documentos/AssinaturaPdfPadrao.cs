using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Regra do sistema (pedido do usuário, 02/10): toda assinatura de documento segue o mesmo padrão —
// nome completo, uma linha logo abaixo e, sob a linha, "Assinado digitalmente" e a legenda
// "Assinado em dd/MM/aaaa HH:mm · método", os dois em corpo 6 e na mesma cor. Todo PDF novo com
// assinatura deve usar este helper em vez de montar o texto por conta própria.
public static class AssinaturaPdfPadrao
{
    public const float TamanhoFonte = 6f;
    public static readonly string Cor = Colors.Grey.Darken2;

    // As duas linhas miúdas (sem nome nem traço), para células de tabela onde o nome já está na
    // coluna ao lado. `assinadoEm` é UTC (padrão do banco) e vira horário de Brasília aqui.
    public static void Legenda(ColumnDescriptor coluna, DateTime assinadoEm, MetodoAutenticacaoAssinatura? metodo)
    {
        coluna.Item().AlignCenter().Text("Assinado digitalmente").FontSize(TamanhoFonte).Italic().FontColor(Cor);
        coluna.Item().AlignCenter()
            .Text(DescricaoMetodoAssinatura.Legenda(HorarioBrasilia.De(assinadoEm), metodo))
            .FontSize(TamanhoFonte).FontColor(Cor);
    }

    // Bloco completo de um assinante, no padrão do certificado (pedido de 30/10: "todo documento que
    // constar assinatura deve sair assim"): [papel] / selo / linha / nome / [cargo]. O selo vinho traz
    // "ASSINADO DIGITALMENTE", nome, data (Brasília) e método, e o hash quando houver; sem assinatura,
    // fica tracejado com "Aguardando assinatura".
    public static void Bloco(IContainer container, string? papel, string? nome, string? funcao, DateTime? assinadoEm,
        MetodoAutenticacaoAssinatura? metodo, string? hash = null)
    {
        container.ShowEntire().Column(coluna =>
        {
            if (!string.IsNullOrWhiteSpace(papel))
                coluna.Item().AlignCenter().Text(papel).FontSize(7.5f).Bold();

            coluna.Item().PaddingTop(4).PaddingHorizontal(6).Element(c => Selo(c, nome, assinadoEm, metodo, hash));
            coluna.Item().PaddingTop(4).LineHorizontal(0.75f).LineColor(Colors.Black);

            // Sem nome (ainda não há quem assine) reserva a altura do nome.
            if (string.IsNullOrWhiteSpace(nome))
                coluna.Item().Height(12);
            else
                coluna.Item().PaddingTop(2).AlignCenter().Text(nome.ToUpperInvariant()).FontSize(8.5f).SemiBold();

            if (!string.IsNullOrWhiteSpace(funcao))
                coluna.Item().AlignCenter().Text(funcao).FontSize(7);
        });
    }

    public const string CorMarca = "#670000";

    public static void Selo(IContainer container, string? nome, DateTime? assinadoEm, MetodoAutenticacaoAssinatura? metodo, string? hash = null)
    {
        if (assinadoEm is not { } quando)
        {
            container.MinHeight(36).Border(0.6f).BorderColor(Colors.Grey.Lighten1).CornerRadius(4).AlignCenter().AlignMiddle()
                .Text("Aguardando assinatura").FontSize(7f).Italic().FontColor(Colors.Grey.Darken1);
            return;
        }

        container.Border(0.8f).BorderColor(CorMarca).CornerRadius(4).Background("#ffffff").Row(linha =>
        {
            linha.ConstantItem(4).Background(CorMarca);
            linha.RelativeItem().PaddingVertical(3).PaddingHorizontal(6).Column(texto =>
            {
                texto.Item().Text("ASSINADO DIGITALMENTE").FontSize(6f).Bold().FontColor(CorMarca).LetterSpacing(0.08f);
                if (!string.IsNullOrWhiteSpace(nome))
                    texto.Item().Text(nome.ToUpperInvariant()).FontSize(7.5f).SemiBold().ClampLines(1);
                texto.Item().Text(DescricaoMetodoAssinatura.Legenda(HorarioBrasilia.De(quando), metodo))
                    .FontSize(TamanhoFonte).FontColor(Cor).ClampLines(1);
                if (!string.IsNullOrWhiteSpace(hash))
                    texto.Item().Text($"Hash SHA-256: {HashCurto(hash)}").FontSize(5.5f).FontColor(Cor).ClampLines(1);
            });
        });
    }

    // 16 primeiros caracteres em blocos de 4 (ex.: 3F9A-12C4-88B0-7D1E).
    public static string HashCurto(string hash)
    {
        var h = hash.ToUpperInvariant();
        var n = Math.Min(16, h.Length);
        return string.Join('-', Enumerable.Range(0, (n + 3) / 4).Select(i => h.Substring(i * 4, Math.Min(4, n - i * 4))));
    }
}
