using AAHBRANT.SST.Application.EscopoPcmso;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Escopo para o médico elaborar o PCMSO (21/10). Documento de trabalho, sem assinatura nem
// validação pública: o PCMSO assinado é o que o médico devolve.
public class EscopoPcmsoPdfService : IEscopoPcmsoPdfService
{
    private const string CorMarca = "#670000";

    public byte[] Gerar(EscopoPcmsoDto e, byte[]? logoObra)
    {
        var geradoEm = HorarioBrasilia.De(e.GeradoEmUtc);
        var base_ = e.PgrNome is null
            ? "Sem PGR vigente"
            : $"Base: {e.PgrNome}" + (e.PgrDataElaboracao is { } d ? $" · elaborado em {d:dd/MM/yyyy}" : "");

        return Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(1.8f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(9.5f));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "Escopo para Elaboração do PCMSO", e.ObraNome, logoObra));

                pagina.Content().PaddingVertical(10).Column(c =>
                {
                    c.Spacing(10);
                    c.Item().Text($"{base_} · Gerado em {geradoEm:dd/MM/yyyy HH:mm}").FontSize(8.5f).FontColor(Colors.Grey.Darken2);

                    Secao(c, "1. Identificação da obra");
                    c.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                        Campo(t, "Obra", e.ObraNome);
                        Campo(t, "CNPJ", e.Cnpj);
                        Campo(t, "Trabalhadores ativos", e.TrabalhadoresAtivos.ToString());
                        Campo(t, "Endereço", e.Endereco);
                        Campo(t, "Início", e.DataInicio?.ToString("dd/MM/yyyy"));
                        Campo(t, "Previsão de término", e.DataPrevisaoTermino?.ToString("dd/MM/yyyy"));
                    });

                    Secao(c, "2. Funções por GHE");
                    Tabela(c, new[] { ("GHE", 1.2f), ("Função", 2.2f), ("Qtde", 0.7f), ("Atividades", 4f) },
                        e.Funcoes.Select(f => new[] { Ghe(f.GheNumero, f.GheSetor), f.Funcao, f.Trabalhadores.ToString(), f.Atividades ?? "—" }),
                        "O PGR não tem GHE com funções cadastradas.");

                    Secao(c, "3. Riscos ocupacionais por GHE (inventário do PGR)");
                    Tabela(c, new[] { ("GHE", 0.7f), ("Tipo", 1.2f), ("Perigo / agente", 2.4f), ("Fonte / atividade", 3.2f), ("Nível", 1f) },
                        e.Riscos.Select(r => new[] { r.GheNumero.ToString("00"), r.Tipo ?? "—", r.Perigo, string.Join(" · ", new[] { r.Fonte, r.Atividade }.Where(s => !string.IsNullOrWhiteSpace(s))), Nivel(r.Nivel) }),
                        "O PGR não tem riscos cadastrados nas atividades dos GHE.");

                    Secao(c, "4. Atividades críticas (exigem aptidão específica no ASO)");
                    if (e.AtividadesCriticas.Count == 0)
                        c.Item().Text("Nenhuma atividade crítica identificada pelos cursos obrigatórios da obra.").Italic();
                    else
                        foreach (var a in e.AtividadesCriticas)
                            c.Item().Text(t =>
                            {
                                t.Span("• " + a.Descricao).SemiBold();
                                t.Span(" — " + string.Join(", ", a.Funcoes));
                            });

                    Secao(c, "5. A cargo do médico responsável");
                    c.Item().Text("Definir os exames por função, a periodicidade, os critérios de aptidão e assinar o PCMSO. " +
                                  "Devolver o documento concluído para anexação na obra (Saúde Ocupacional › PCMSO).").Justify();
                });

                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("AAHBRANT SST · Escopo para elaboração do PCMSO · página ").FontSize(7);
                    t.CurrentPageNumber().FontSize(7);
                    t.Span(" de ").FontSize(7);
                    t.TotalPages().FontSize(7);
                });
            });
        }).GeneratePdf();
    }

    private static string Ghe(int numero, string? setor) => string.IsNullOrWhiteSpace(setor) ? numero.ToString("00") : $"{numero:00} · {setor}";

    private static string Nivel(NivelRisco n) => n switch
    {
        NivelRisco.Trivial => "Trivial",
        NivelRisco.Baixo => "Baixo",
        NivelRisco.Moderado => "Moderado",
        NivelRisco.Alto => "Alto",
        NivelRisco.Critico => "Crítico",
        _ => n.ToString(),
    };

    private static void Secao(ColumnDescriptor c, string titulo) =>
        c.Item().PaddingTop(6).Text(titulo).FontSize(11.5f).Bold().FontColor(CorMarca);

    private static void Campo(TableDescriptor t, string rotulo, string? valor) =>
        t.Cell().PaddingBottom(4).Text(x =>
        {
            x.Line(rotulo).FontSize(8).FontColor(Colors.Grey.Darken1);
            if (string.IsNullOrWhiteSpace(valor)) x.Span("não informado").Italic().FontColor(Colors.Orange.Darken3);
            else x.Span(valor).SemiBold();
        });

    private static void Tabela(ColumnDescriptor c, (string Titulo, float Peso)[] colunas, IEnumerable<string[]> linhas, string vazio)
    {
        var lista = linhas.ToList();
        if (lista.Count == 0)
        {
            c.Item().Text(vazio).Italic();
            return;
        }
        c.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { foreach (var col in colunas) cd.RelativeColumn(col.Peso); });
            t.Header(h =>
            {
                foreach (var col in colunas)
                    h.Cell().Background(CorMarca).Padding(4).Text(col.Titulo).FontSize(8.5f).Bold().FontColor(Colors.White);
            });
            foreach (var linha in lista)
                foreach (var valor in linha)
                    t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(valor).FontSize(8.5f);
        });
    }
}
