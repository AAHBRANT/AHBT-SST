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

    // Bloco completo de um assinante: [papel] / nome completo / linha / [cargo] / assinatura miúda.
    // Sem assinatura ainda, mostra "Aguardando assinatura" no lugar da legenda.
    public static void Bloco(IContainer container, string? papel, string nome, string? funcao, DateTime? assinadoEm, MetodoAutenticacaoAssinatura? metodo)
    {
        container.ShowEntire().Column(coluna =>
        {
            if (!string.IsNullOrWhiteSpace(papel))
                coluna.Item().AlignCenter().Text(papel).FontSize(7.5f).Bold();

            coluna.Item().PaddingTop(14).AlignCenter().Text(nome).FontSize(9).SemiBold();
            coluna.Item().PaddingTop(2).LineHorizontal(0.75f).LineColor(Colors.Black);

            if (!string.IsNullOrWhiteSpace(funcao))
                coluna.Item().PaddingTop(2).AlignCenter().Text(funcao).FontSize(7).SemiBold();

            if (assinadoEm is { } quando)
                Legenda(coluna, quando, metodo);
            else
                coluna.Item().AlignCenter().Text("Aguardando assinatura").FontSize(TamanhoFonte).Italic().FontColor(Cor);
        });
    }
}
