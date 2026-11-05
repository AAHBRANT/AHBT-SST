using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Grade padrão dos dados de identificação dos documentos (pedido do usuário, 05/11): nada de
// "Rótulo: valor" solto em linhas de texto. Cada campo é uma célula com borda, rótulo miúdo em
// maiúsculas em cima e o valor embaixo — o mesmo padrão da Investigação de Ocorrência.
public static class GradeDocumentoPadrao
{
    public const string CorLinha = "#CFC9C4";
    public const string CorRotulo = "#5C5652";
    public const string CorMarca = "#670000";

    // Campo da grade: Colunas = quantas das 4 colunas ocupa (1, 2, 3 ou 4).
    public record Campo(string Rotulo, string? Valor, int Colunas = 1, bool Destaque = false);

    public static void Desenhar(IContainer container, IEnumerable<Campo> campos, int totalColunas = 4, float tamanhoValor = 10)
    {
        container.Table(tabela =>
        {
            tabela.ColumnsDefinition(c =>
            {
                for (var i = 0; i < totalColunas; i++) c.RelativeColumn();
            });

            foreach (var campo in campos)
                Celula(tabela, campo, tamanhoValor);
        });
    }

    public static void Celula(TableDescriptor tabela, Campo campo, float tamanhoValor = 10)
    {
        tabela.Cell().ColumnSpan((uint)campo.Colunas).Border(0.5f).BorderColor(CorLinha)
            .PaddingVertical(3).PaddingHorizontal(6).Column(celula =>
            {
                celula.Item().Text(campo.Rotulo.ToUpperInvariant()).FontSize(6.5f).FontColor(CorRotulo).LetterSpacing(0.04f);
                var texto = celula.Item().Text(string.IsNullOrWhiteSpace(campo.Valor) ? "—" : campo.Valor).FontSize(tamanhoValor).SemiBold();
                if (campo.Destaque) texto.FontColor(CorMarca);
            });
    }
}
