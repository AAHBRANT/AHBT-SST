using System.Globalization;
using System.Text;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Trabalhadores;

// Página de resumo do Relatório de Fiscalização (pedido do usuário, 06/10): os 4 cards e os gráficos
// do perfil do trabalhador (aba Geral + EPI + Treinamentos) passam a sair também no PDF. Usa os mesmos
// números e regras da tela (PerfilGeralTab.tsx) — nada novo é consultado no banco. As roscas são SVG
// só com traços (sem texto, para não depender de fonte); os números do centro e as barras são
// elementos nativos do QuestPDF.
public static class ResumoGraficosFiscalizacao
{
    private const string CorMarca = "#670000";
    private const string CorOk = "#2e7d4f";
    private const string CorAtencao = "#c08a12";
    private const string CorAlerta = "#b3261e";
    private const string CorInfo = "#3a5a80";
    private const string CorTrilho = "#eee9e2";
    private const string CorLinha = "#e4dfd6";
    private const string CorTextoSuave = "#6f6666";

    // Mesmo limiar de PerfilGeralTab.tsx (DIAS_ALERTA_VENCIMENTO_EPI).
    public const int DiasAlertaVencimentoEpi = 30;
    private const int MaximoItensRanking = 6;

    public record FatiaRosca(string Rotulo, int Valor, string Cor);

    public static (int EmDia, int Vencendo, int Vencido) ContarStatusEpis(PerfilCompletoTrabalhadorDto perfil, DateTime hoje)
    {
        int emDia = 0, vencendo = 0, vencido = 0;
        foreach (var epi in perfil.EpisAtivos)
        {
            if (epi.DataValidade is null) { emDia++; continue; }
            var dias = (epi.DataValidade.Value.Date - hoje.Date).Days;
            if (dias < 0) vencido++;
            else if (dias <= DiasAlertaVencimentoEpi) vencendo++;
            else emDia++;
        }
        return (emDia, vencendo, vencido);
    }

    public static string DescreverMotivo(MotivoEntregaEpi motivo) => motivo switch
    {
        MotivoEntregaEpi.Inicial => "Entrega inicial",
        MotivoEntregaEpi.Dano => "Dano",
        MotivoEntregaEpi.Extravio => "Extravio",
        MotivoEntregaEpi.Vencimento => "Vencimento",
        MotivoEntregaEpi.TrocaDeFuncao => "Troca de função",
        _ => motivo.ToString(),
    };

    public static void Desenhar(IContainer container, PerfilCompletoTrabalhadorDto perfil, DateTime hoje)
    {
        var (emDia, vencendo, vencido) = ContarStatusEpis(perfil, hoje);
        var assiduidade = perfil.AssiduidadeDds;
        var percentualDds = assiduidade.TotalRealizados > 0
            ? (int)Math.Round(assiduidade.TotalParticipados * 100.0 / assiduidade.TotalRealizados, MidpointRounding.AwayFromZero)
            : (int?)null;
        var treinamentosValidos = perfil.Treinamentos.Count(t => t.DataValidade.Date >= hoje.Date);
        var episAtivos = perfil.EpisAtivos.Count;

        container.Column(coluna =>
        {
            coluna.Spacing(8);
            coluna.Item().Text("Resumo").FontSize(13).Bold().FontColor(CorMarca);

            coluna.Item().Row(linha =>
            {
                linha.Spacing(6);
                linha.RelativeItem().Element(c => Card(c, "EPIs ativos", $"{episAtivos} {(episAtivos == 1 ? "item" : "itens")}", CorInfo));
                linha.RelativeItem().Element(c => Card(c, "Presença em DDS",
                    percentualDds is null ? "—" : $"{assiduidade.TotalParticipados}/{assiduidade.TotalRealizados} ({percentualDds}%)", CorOk));
                linha.RelativeItem().Element(c => Card(c, "Trocas de EPI (ano)",
                    $"{perfil.TrocasNoAno} {(perfil.TrocasNoAno == 1 ? "solicitação" : "solicitações")}", CorAtencao));
                linha.RelativeItem().Element(c => Card(c, "Treinamentos válidos",
                    $"{treinamentosValidos} {(treinamentosValidos == 1 ? "curso" : "cursos")}", CorInfo));
            });

            coluna.Item().Row(linha =>
            {
                linha.Spacing(6);
                linha.RelativeItem().Element(c => Painel(c, "Status dos EPIs", "Validade dos itens em posse do trabalhador", p =>
                {
                    if (episAtivos == 0) { Vazio(p, "Nenhum EPI ativo."); return; }
                    Rosca(p, "EPIs ativos", episAtivos, new[]
                    {
                        new FatiaRosca("Em dia", emDia, CorOk),
                        new FatiaRosca("Vencendo", vencendo, CorAtencao),
                        new FatiaRosca("Vencido", vencido, CorAlerta),
                    });
                }));
                linha.RelativeItem().Element(c => Painel(c, "Assiduidade em DDS", "Desde a admissão, na obra", p =>
                {
                    if (assiduidade.TotalRealizados == 0) { Vazio(p, "Nenhum DDS realizado na obra desde a admissão."); return; }
                    Rosca(p, "DDS realizados", assiduidade.TotalRealizados, new[]
                    {
                        new FatiaRosca("Participou", assiduidade.TotalParticipados, CorOk),
                        new FatiaRosca("Não participou", Math.Max(assiduidade.TotalRealizados - assiduidade.TotalParticipados, 0), CorAlerta),
                    });
                }));
            });

            coluna.Item().Row(linha =>
            {
                linha.Spacing(6);
                linha.RelativeItem().Element(c => Painel(c, "Motivo das trocas (ano)", $"Reposições de EPI em {hoje.Year}", p =>
                {
                    var total = perfil.MotivosTroca.Sum(m => m.Quantidade);
                    if (perfil.MotivosTroca.Count == 0 || total == 0) { Vazio(p, "Nenhuma troca registrada este ano."); return; }
                    p.Column(col =>
                    {
                        col.Spacing(5);
                        foreach (var m in perfil.MotivosTroca)
                        {
                            var pct = (int)Math.Round(m.Quantidade * 100.0 / total, MidpointRounding.AwayFromZero);
                            col.Item().Element(b => Barra(b, DescreverMotivo(m.Motivo), $"{pct}% ({m.Quantidade})", pct));
                        }
                    });
                }));
                linha.RelativeItem().Element(c => Painel(c, "Frequência de trocas por EPI", "Entregas por item do catálogo", p =>
                {
                    var itens = perfil.FrequenciaTrocas.OrderByDescending(f => f.QuantidadeTrocas).ThenBy(f => f.CatalogoEpiNome).ToList();
                    if (itens.Count == 0) { Vazio(p, "Sem dados de troca de EPI para exibir."); return; }
                    var maximo = Math.Max(itens[0].QuantidadeTrocas, 1);
                    p.Column(col =>
                    {
                        col.Spacing(5);
                        foreach (var item in itens.Take(MaximoItensRanking))
                        {
                            var pct = (int)Math.Round(item.QuantidadeTrocas * 100.0 / maximo, MidpointRounding.AwayFromZero);
                            col.Item().Element(b => Barra(b, item.CatalogoEpiNome,
                                $"{item.QuantidadeTrocas} {(item.QuantidadeTrocas == 1 ? "troca" : "trocas")}", pct));
                        }
                        if (itens.Count > MaximoItensRanking)
                            col.Item().Text($"+ {itens.Count - MaximoItensRanking} item(ns) com menos trocas").FontSize(8).FontColor(CorTextoSuave);
                    });
                }));
            });
        });
    }

    private static void Card(IContainer container, string rotulo, string valor, string cor)
    {
        container.Border(0.7f).BorderColor(CorLinha).Column(col =>
        {
            col.Item().Height(3).Background(cor);
            col.Item().Padding(7).Column(c =>
            {
                c.Spacing(3);
                c.Item().Text(rotulo).FontSize(8).FontColor(CorTextoSuave);
                c.Item().Text(valor).FontSize(12).Bold();
            });
        });
    }

    private static void Painel(IContainer container, string titulo, string legenda, Action<IContainer> conteudo)
    {
        container.Border(0.7f).BorderColor(CorLinha).Padding(8).Column(col =>
        {
            col.Spacing(6);
            col.Item().Column(cab =>
            {
                cab.Item().Text(titulo).FontSize(10).Bold();
                cab.Item().Text(legenda).FontSize(8).FontColor(CorTextoSuave);
            });
            col.Item().Element(conteudo);
        });
    }

    private static void Vazio(IContainer container, string texto) =>
        container.Text(texto).FontSize(9).Italic().FontColor(CorTextoSuave);

    private static void Barra(IContainer container, string rotulo, string valor, int percentual)
    {
        var pct = Math.Clamp(percentual, 0, 100);
        container.Column(col =>
        {
            col.Spacing(2);
            col.Item().Row(r =>
            {
                r.RelativeItem().Text(rotulo).FontSize(9);
                r.AutoItem().Text(valor).FontSize(9).FontColor(CorTextoSuave);
            });
            col.Item().Height(6).Background(CorTrilho).Row(r =>
            {
                if (pct > 0) r.RelativeItem(pct).Background(CorMarca);
                if (pct < 100) r.RelativeItem(100 - pct);
            });
        });
    }

    private static void Rosca(IContainer container, string legendaCentral, int total, IReadOnlyList<FatiaRosca> fatias)
    {
        container.Row(linha =>
        {
            linha.Spacing(10);
            linha.ConstantItem(86).Height(86).Layers(camadas =>
            {
                camadas.Layer().Svg(GerarSvgRosca(fatias));
                camadas.PrimaryLayer().AlignCenter().AlignMiddle().Column(c =>
                {
                    c.Item().AlignCenter().Text(total.ToString(CultureInfo.InvariantCulture)).FontSize(15).Bold();
                    c.Item().AlignCenter().Text(legendaCentral).FontSize(6.5f).FontColor(CorTextoSuave);
                });
            });
            linha.RelativeItem().AlignMiddle().Column(c =>
            {
                c.Spacing(4);
                foreach (var fatia in fatias)
                {
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(8).PaddingTop(2).Height(8).Background(fatia.Cor);
                        r.ConstantItem(5);
                        r.RelativeItem().Text($"{fatia.Rotulo}: {fatia.Valor}").FontSize(9);
                    });
                }
            });
        });
    }

    // SVG da rosca só com traços (arcos). Sem texto: números e rótulos ficam no QuestPDF.
    public static string GerarSvgRosca(IReadOnlyList<FatiaRosca> fatias)
    {
        const double raio = 15.9;
        const double espessura = 6;
        var inv = CultureInfo.InvariantCulture;
        var total = fatias.Sum(f => f.Valor);

        var svg = new StringBuilder();
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 42 42\">");
        svg.Append(string.Format(inv, "<circle cx=\"21\" cy=\"21\" r=\"{0}\" fill=\"none\" stroke=\"{1}\" stroke-width=\"{2}\"/>", raio, CorTrilho, espessura));

        var visiveis = fatias.Where(f => f.Valor > 0).ToList();
        if (total > 0 && visiveis.Count == 1)
        {
            svg.Append(string.Format(inv, "<circle cx=\"21\" cy=\"21\" r=\"{0}\" fill=\"none\" stroke=\"{1}\" stroke-width=\"{2}\"/>", raio, visiveis[0].Cor, espessura));
        }
        else if (total > 0)
        {
            var anguloInicial = -90.0;
            foreach (var fatia in visiveis)
            {
                var varredura = 360.0 * fatia.Valor / total;
                var anguloFinal = anguloInicial + varredura;
                var (x1, y1) = Ponto(raio, anguloInicial);
                var (x2, y2) = Ponto(raio, anguloFinal);
                var grande = varredura > 180 ? 1 : 0;
                svg.Append(string.Format(inv,
                    "<path d=\"M {0:0.###} {1:0.###} A {2} {2} 0 {3} 1 {4:0.###} {5:0.###}\" fill=\"none\" stroke=\"{6}\" stroke-width=\"{7}\"/>",
                    x1, y1, raio, grande, x2, y2, fatia.Cor, espessura));
                anguloInicial = anguloFinal;
            }
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    private static (double X, double Y) Ponto(double raio, double graus)
    {
        var rad = graus * Math.PI / 180.0;
        return (21 + raio * Math.Cos(rad), 21 + raio * Math.Sin(rad));
    }
}
