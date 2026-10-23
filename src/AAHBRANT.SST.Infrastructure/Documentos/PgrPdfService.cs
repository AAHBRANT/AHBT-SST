using AAHBRANT.SST.Application.GeracaoPgr;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// PDF do PGR gerado pela EAP (23/10/2026). Layout no padrão dos demais documentos do sistema: cabeçalho
// com a logo cadastrada na obra, seções numeradas, tabelas com cabeçalho em vinho e rodapé com paginação.
// Só riscos não excluídos e medidas aceitas entram no documento.
public class PgrPdfService : IPgrPdfService
{
    private const string CorMarca = "#670000";
    private const string CorTextoSecundario = "#4a4a4a";

    public byte[] Gerar(DadosPgrPdf d, byte[]? logoObra)
    {
        var elaboradoEm = HorarioBrasilia.De(d.GeradoEmUtc);

        return Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(1.8f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(9.5f).FontColor(Colors.Black));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "Programa de Gerenciamento de Riscos (PGR)", d.ObraNome, logoObra));

                pagina.Content().PaddingVertical(10).Column(c =>
                {
                    c.Spacing(8);
                    c.Item().Text(d.Nome).FontSize(14).Bold().FontColor(CorMarca);
                    c.Item().Text($"Documento gerado pela EAP da obra · emitido em {elaboradoEm:dd/MM/yyyy HH:mm}")
                        .FontSize(8.5f).FontColor(CorTextoSecundario);

                    Secao(c, "1. Identificação");
                    c.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                        Campo(t, "Obra", d.ObraNome);
                        Campo(t, "CNPJ", d.ObraCnpj);
                        Campo(t, "Elaborado em", d.DataElaboracao.ToString("dd/MM/yyyy"));
                        Campo(t, "Próxima revisão", d.DataProximaRevisao?.ToString("dd/MM/yyyy"));
                        Campo(t, "Elaborado por", d.ElaboradoPorNome);
                        Campo(t, "EAP de referência", d.NomeArquivoEap);
                    });

                    Secao(c, "2. Grupos homogêneos de exposição (GHE)");
                    Tabela(c, new[] { ("GHE", 0.8f), ("Setor / ambiente", 2.6f), ("Jornada", 2.4f), ("Funções", 0.9f), ("Atividades", 1f) },
                        d.Estrutura.Ghes.OrderBy(g => g.Numero).Select(g => new[]
                        {
                            g.Numero.ToString("00"), g.Nome, g.Jornada ?? "—", g.Funcoes.Count.ToString(), g.Atividades.Count.ToString(),
                        }),
                        "Nenhum GHE definido.");

                    Secao(c, "3. Funções e descrição das atividades por GHE");
                    var funcoes = d.Estrutura.Ghes.OrderBy(g => g.Numero)
                        .SelectMany(g => g.Funcoes.Select(f => new { g.Numero, f.Nome, f.Descricao })).ToList();
                    Tabela(c, new[] { ("GHE", 0.8f), ("Função", 2.2f), ("Descrição das atividades", 5f) },
                        funcoes.Select(f => new[] { f.Numero.ToString("00"), f.Nome, f.Descricao ?? "—" }),
                        "Nenhuma função cadastrada nos GHE.");

                    Secao(c, "4. Inventário de riscos e medidas de prevenção");
                    c.Item().Text("P = probabilidade · S = severidade · P×S = nível de risco na matriz da organização.")
                        .FontSize(8).FontColor(CorTextoSecundario).Italic();
                    var haRiscos = false;
                    foreach (var ghe in d.Estrutura.Ghes.OrderBy(g => g.Numero))
                    {
                        foreach (var atividade in ghe.Atividades.Where(a => a.Riscos.Any(r => !r.Excluido)))
                        {
                            haRiscos = true;
                            c.Item().PaddingTop(4).Text($"GHE {ghe.Numero:00} · EAP {atividade.CodigoEap} — {atividade.Nome}")
                                .FontSize(9).SemiBold();
                            Tabela(c, new[] { ("Perigo", 2.2f), ("Tipo", 1.2f), ("Danos", 2.4f), ("P", 0.5f), ("S", 0.5f), ("P×S", 0.6f), ("Medidas aceitas", 4.2f) },
                                atividade.Riscos.Where(r => !r.Excluido).Select(r => new[]
                                {
                                    r.Perigo, r.Tipo, r.Danos ?? "—", r.Probabilidade.ToString(), r.Severidade.ToString(),
                                    (r.Probabilidade * r.Severidade).ToString(), Medidas(r),
                                }),
                                "Nenhum risco nesta atividade.");
                        }
                    }
                    if (!haRiscos)
                        c.Item().Text("Nenhum risco inventariado.").Italic();

                    Secao(c, "5. Plano de ação");
                    if (d.Estrutura.PlanoAcao.Count == 0)
                        c.Item().Text("Nenhuma ação registrada.").Italic();
                    else
                        for (var i = 0; i < d.Estrutura.PlanoAcao.Count; i++)
                            c.Item().Text($"{i + 1}. {d.Estrutura.PlanoAcao[i]}").Justify();

                    Secao(c, "6. Aprovação");
                    c.Item().Text(t =>
                    {
                        t.Span("Aprovado pelo Engenheiro de Segurança do Trabalho: ");
                        t.Span(d.AprovadoPorNome ?? "não informado").SemiBold();
                        if (d.AprovadoEmUtc is { } aprovadoEm)
                            t.Span($" em {HorarioBrasilia.De(aprovadoEm):dd/MM/yyyy HH:mm}.");
                    });
                    c.Item().PaddingTop(28).Row(linha =>
                    {
                        linha.RelativeItem().BorderTop(0.6f).PaddingTop(4).AlignCenter()
                            .Text("Engenheiro de Segurança do Trabalho").FontSize(8.5f);
                        linha.ConstantItem(40);
                        linha.RelativeItem().BorderTop(0.6f).PaddingTop(4).AlignCenter()
                            .Text("Responsável pela obra").FontSize(8.5f);
                    });
                });

                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("AAHBRANT SST · PGR · página ").FontSize(7);
                    t.CurrentPageNumber().FontSize(7);
                    t.Span(" de ").FontSize(7);
                    t.TotalPages().FontSize(7);
                });
            });
        }).GeneratePdf();
    }

    // Só as medidas aceitas pelo Engenheiro, com a fase (Antes, Durante, Após) quando houver texto.
    private static string Medidas(RiscoGerado r)
    {
        var linhas = r.Medidas
            .Where(m => m.Decisao == "Aceita" && !string.IsNullOrWhiteSpace(m.Texto))
            .Select(m => $"{NomeFase(m.Fase)}: {m.Texto.Trim()}")
            .ToList();
        return linhas.Count == 0 ? "—" : string.Join("\n", linhas);
    }

    private static string NomeFase(int fase) => fase switch
    {
        1 => "Antes",
        2 => "Durante",
        3 => "Após",
        _ => "Medida",
    };

    private static void Secao(ColumnDescriptor c, string titulo) =>
        c.Item().PaddingTop(6).Text(titulo).FontSize(11.5f).Bold().FontColor(CorMarca);

    private static void Campo(TableDescriptor t, string rotulo, string? valor) =>
        t.Cell().PaddingBottom(4).Text(x =>
        {
            x.Line(rotulo).FontSize(8).FontColor(CorTextoSecundario);
            if (string.IsNullOrWhiteSpace(valor)) x.Span("não informado").Italic().FontColor(Colors.Orange.Darken3);
            else x.Span(valor).SemiBold();
        });

    private static void Tabela(ColumnDescriptor c, (string Titulo, float Peso)[] colunas, IEnumerable<string[]> linhas, string vazio)
    {
        var lista = linhas.ToList();
        if (lista.Count == 0)
        {
            c.Item().Text(vazio).Italic().FontColor(CorTextoSecundario);
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
