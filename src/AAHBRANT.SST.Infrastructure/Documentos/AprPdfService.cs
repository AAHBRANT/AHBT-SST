using AAHBRANT.SST.Application.Aprs;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Reproduz o layout do formulário "APR – ANÁLISE PRELIMINAR DE RISCO | REV.02" (planilha do
// usuário, 2026-08-29): cabeçalho, envolvidos/equipe exposta, tabela de etapas/riscos (com risco
// inicial e residual coloridos exatamente como a formatação condicional da planilha), textos fixos
// de recomendações/paralisação, e as duas assinaturas formais do rodapé. Paisagem (A4) por causa das
// 13 colunas da tabela principal — a planilha original também é mais larga que alta.
public class AprPdfService : IAprPdfService
{
    private const string CorMarca = "#670000";
    private const string CorCritico = "#C00000";
    private const string CorAlto = "#F4B183";
    private const string CorModerado = "#FFD966";
    private const string CorBaixo = "#A9D18E";

    // "RECOMENDAÇÕES" e "PARALISAÇÃO" — texto fixo, idêntico ao rodapé do formulário original.
    private const string TextoRecomendacoes =
        "RECOMENDAÇÕES: realizar DDS/ciência da APR antes do início; verificar EPIs/EPCs, condições do local, " +
        "máquinas/equipamentos e interferências. Revisar a APR quando houver mudança de atividade/processo/condição, " +
        "acidente/incidente relevante, novo perigo ou ineficácia das medidas.";

    private const string TextoParalisacao =
        "PARALISAÇÃO: Ao identificar risco grave e iminente ou risco não previsto, interromper a atividade e comunicar " +
        "imediatamente à liderança. A retomada somente ocorrerá após avaliação e implementação das medidas necessárias.";

    public byte[] Gerar(AprPdfModelo modelo)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4.Landscape());
                pagina.Margin(1.2f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(8));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "APR – ANÁLISE PRELIMINAR DE RISCO | REV.02", modelo.ObraNome, modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(10).Column(coluna =>
                {
                    coluna.Spacing(8);

                    coluna.Item().Element(c => SecaoCabecalho(c, modelo));
                    coluna.Item().Element(c => SecaoEnvolvidos(c, modelo));
                    coluna.Item().Element(c => SecaoRiscos(c, modelo));
                    coluna.Item().Text(TextoRecomendacoes).FontSize(7);
                    coluna.Item().Text(TextoParalisacao).FontSize(7).Bold();
                    coluna.Item().Element(c => SecaoAssinaturas(c, modelo));
                    coluna.Item().Element(SecaoMatrizCriterios);
                });

                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(7);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7);
                });
            });
        });

        return documento.GeneratePdf();
    }

    private static void SecaoCabecalho(IContainer container, AprPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem(2).Text(t => { t.Span("OBRA / CONTRATO: ").SemiBold(); t.Span(modelo.ObraNome ?? "não informado"); });
                linha.RelativeItem().Text(t => { t.Span("Nº APR: ").SemiBold(); t.Span(modelo.NumeroApr ?? "-"); });
                linha.RelativeItem().Text(t => { t.Span("DATA ELAB.: ").SemiBold(); t.Span(modelo.Data.ToString("dd/MM/yyyy")); });
            });
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem(2).Text(t => { t.Span("ATIVIDADE: ").SemiBold(); t.Span(modelo.AtividadeNome); });
                linha.RelativeItem().Text(t => { t.Span("LOCAL / FRENTE: ").SemiBold(); t.Span(modelo.Local); });
            });
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem(2).Text(t => { t.Span("MÁQUINAS / EQUIP.: ").SemiBold(); t.Span(modelo.MaquinasEquipamentos ?? "-"); });
                linha.RelativeItem().Text(t => { t.Span("PGR / PROCEDIMENTO REF.: ").SemiBold(); t.Span(modelo.PgrReferencia ?? "-"); });
            });
        });
    }

    private static void SecaoEnvolvidos(IContainer container, AprPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Background(CorMarca).Padding(3).Text("ENVOLVIDOS NA ATIVIDADE / EQUIPE EXPOSTA")
                .FontColor(Colors.White).Bold().FontSize(9);

            if (modelo.Envolvidos.Count == 0)
            {
                coluna.Item().Text("Nenhum envolvido cadastrado.").Italic();
                return;
            }

            coluna.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1);
                });
                table.Header(header =>
                {
                    header.Cell().Element(CabecalhoCelula).Text("Nome");
                    header.Cell().Element(CabecalhoCelula).Text("Função");
                    header.Cell().Element(CabecalhoCelula).Text("Ass./Visto");
                });
                foreach (var envolvido in modelo.Envolvidos)
                {
                    table.Cell().Element(Celula).Text(envolvido.Nome);
                    table.Cell().Element(Celula).Text(envolvido.Funcao ?? "-");
                    table.Cell().Element(Celula).Text(envolvido.Assinou ? "Assinado" : "Pendente");
                }
            });
        });
    }

    private static void SecaoRiscos(IContainer container, AprPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.6f); // Etapa
                    columns.RelativeColumn(1.6f); // Perigo
                    columns.RelativeColumn(1.8f); // Fonte
                    columns.RelativeColumn(1.8f); // Lesões
                    columns.RelativeColumn(1.4f); // Expostos
                    columns.ConstantColumn(16); // P
                    columns.ConstantColumn(16); // S
                    columns.ConstantColumn(50); // Risco inicial
                    columns.RelativeColumn(2.2f); // Medidas
                    columns.RelativeColumn(1.2f); // Responsável
                    columns.ConstantColumn(20); // P res.
                    columns.ConstantColumn(20); // S res.
                    columns.ConstantColumn(50); // Risco residual
                });

                table.Header(header =>
                {
                    header.Cell().Element(CabecalhoCelula).Text("ETAPA DA ATIVIDADE");
                    header.Cell().Element(CabecalhoCelula).Text("PERIGO / EVENTO PERIGOSO");
                    header.Cell().Element(CabecalhoCelula).Text("FONTE / CIRCUNSTÂNCIA");
                    header.Cell().Element(CabecalhoCelula).Text("POSSÍVEIS LESÕES / AGRAVOS / DANOS");
                    header.Cell().Element(CabecalhoCelula).Text("TRABALHADORES EXPOSTOS");
                    header.Cell().Element(CabecalhoCelula).Text("P");
                    header.Cell().Element(CabecalhoCelula).Text("S");
                    header.Cell().Element(CabecalhoCelula).Text("RISCO INICIAL");
                    header.Cell().Element(CabecalhoCelula).Text("MEDIDAS DE PREVENÇÃO / CONTROLE");
                    header.Cell().Element(CabecalhoCelula).Text("RESPONSÁVEL");
                    header.Cell().Element(CabecalhoCelula).Text("P RES.");
                    header.Cell().Element(CabecalhoCelula).Text("S RES.");
                    header.Cell().Element(CabecalhoCelula).Text("RISCO RESIDUAL");
                });

                foreach (var linha in modelo.Riscos)
                {
                    table.Cell().Element(Celula).Text(linha.Etapa);
                    table.Cell().Element(Celula).Text(linha.PerigoEventoPerigoso);
                    table.Cell().Element(Celula).Text(linha.FonteCircunstancia ?? "-");
                    table.Cell().Element(Celula).Text(linha.PossiveisLesoes ?? "-");
                    table.Cell().Element(Celula).Text(linha.TrabalhadoresExpostos ?? "-");
                    table.Cell().Element(Celula).AlignCenter().Text(linha.ProbabilidadeInicial.ToString());
                    table.Cell().Element(Celula).AlignCenter().Text(linha.SeveridadeInicial.ToString());
                    table.Cell().Element(CelulaNivelRisco(linha.NivelRiscoInicial)).AlignCenter()
                        .Text(RotuloNivelRisco(linha.NivelRiscoInicial)).FontSize(6.5f).Bold();
                    table.Cell().Element(Celula).Text(linha.MedidasPrevencao ?? "-");
                    table.Cell().Element(Celula).Text(linha.Responsavel ?? "-");
                    table.Cell().Element(Celula).AlignCenter().Text(linha.ProbabilidadeResidual.ToString());
                    table.Cell().Element(Celula).AlignCenter().Text(linha.SeveridadeResidual.ToString());
                    table.Cell().Element(CelulaNivelRisco(linha.NivelRiscoResidual)).AlignCenter()
                        .Text(RotuloNivelRisco(linha.NivelRiscoResidual)).FontSize(6.5f).Bold();
                }
            });
        });
    }

    private static void SecaoAssinaturas(IContainer container, AprPdfModelo modelo)
    {
        container.Row(linha =>
        {
            linha.RelativeItem().Element(c => BlocoAssinatura(c, "ELABORAÇÃO / SST / RESPONSÁVEL TÉCNICO", modelo.Elaboracao));
            linha.ConstantItem(16);
            linha.RelativeItem().Element(c => BlocoAssinatura(c, "SUPERVISÃO / ENCARREGADO / ENGENHARIA", modelo.Supervisao));
        });
    }

    private static void BlocoAssinatura(IContainer container, string titulo, AprPdfAssinatura assinatura)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(coluna =>
        {
            coluna.Item().Background(CorMarca).Padding(3).Text(titulo).FontColor(Colors.White).Bold().FontSize(8);
            coluna.Item().PaddingTop(4).Text($"Nome: {assinatura.Nome ?? "____________________________"}");
            coluna.Item().Text($"Função: {assinatura.Funcao ?? "____________________________"}");
            coluna.Item().Text("Assinatura: ______________________");
            coluna.Item().Text($"Data: {(assinatura.Data.HasValue ? assinatura.Data.Value.ToString("dd/MM/yyyy") : "____/____/______")}");
        });
    }

    private static void SecaoMatrizCriterios(IContainer container)
    {
        container.Column(coluna =>
        {
            coluna.Item().PaddingTop(6).Background(CorMarca).Padding(3)
                .Text("CRITÉRIOS DA MATRIZ DE RISCO – APR REV.02").FontColor(Colors.White).Bold().FontSize(9);

            coluna.Item().Row(linha =>
            {
                linha.RelativeItem().Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.ConstantColumn(80); columns.RelativeColumn(); });
                    table.Header(header =>
                    {
                        header.Cell().Element(CabecalhoCelula).Text("PROBABILIDADE (P)");
                        header.Cell().Element(CabecalhoCelula).Text("CRITÉRIO");
                    });
                    foreach (var (valor, criterio) in new[]
                    {
                        (1, "Rara"), (2, "Improvável"), (3, "Possível"), (4, "Provável"), (5, "Muito provável"),
                    })
                    {
                        table.Cell().Element(Celula).AlignCenter().Text(valor.ToString());
                        table.Cell().Element(Celula).Text(criterio);
                    }
                });

                linha.ConstantItem(16);

                linha.RelativeItem().Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.ConstantColumn(80); columns.RelativeColumn(); });
                    table.Header(header =>
                    {
                        header.Cell().Element(CabecalhoCelula).Text("SEVERIDADE (S)");
                        header.Cell().Element(CabecalhoCelula).Text("CRITÉRIO");
                    });
                    foreach (var (valor, criterio) in new[]
                    {
                        (1, "Insignificante"), (2, "Leve"), (3, "Moderada"), (4, "Grave"), (5, "Catastrófica / fatalidade"),
                    })
                    {
                        table.Cell().Element(Celula).AlignCenter().Text(valor.ToString());
                        table.Cell().Element(Celula).Text(criterio);
                    }
                });
            });

            coluna.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(80);
                    columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    header.Cell().Element(CabecalhoCelula).Text("P × S");
                    header.Cell().Element(CabecalhoCelula).Text("CLASSIFICAÇÃO");
                    header.Cell().Element(CabecalhoCelula).Text("AÇÃO");
                });
                foreach (var (faixa, nivel, acao) in new[]
                {
                    ("1 a 4", NivelRiscoApr.Baixo, "Manter controles"),
                    ("5 a 9", NivelRiscoApr.Moderado, "Confirmar controles antes/durante"),
                    ("10 a 15", NivelRiscoApr.Alto, "Não iniciar sem controles adicionais"),
                    ("16 a 25", NivelRiscoApr.Critico, "Não iniciar/continuar até redução"),
                })
                {
                    table.Cell().Element(Celula).Text(faixa);
                    table.Cell().Element(CelulaNivelRisco(nivel)).AlignCenter().Text(RotuloNivelRisco(nivel)).Bold();
                    table.Cell().Element(Celula).Text(acao);
                }
            });
        });
    }

    private static IContainer CabecalhoCelula(IContainer container) =>
        container.Background(Colors.Grey.Lighten3).Padding(3).BorderBottom(1).BorderColor(CorMarca).DefaultTextStyle(t => t.FontSize(6.5f).Bold());

    private static IContainer Celula(IContainer container) =>
        container.Padding(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).DefaultTextStyle(t => t.FontSize(7));

    // Cores idênticas à formatação condicional da planilha original (dxf 0-3 do arquivo enviado
    // pelo usuário): Crítico #C00000 (texto branco), Alto #F4B183, Moderado #FFD966, Baixo #A9D18E.
    private static Func<IContainer, IContainer> CelulaNivelRisco(NivelRiscoApr nivel)
    {
        var cor = nivel switch
        {
            NivelRiscoApr.Critico => CorCritico,
            NivelRiscoApr.Alto => CorAlto,
            NivelRiscoApr.Moderado => CorModerado,
            _ => CorBaixo,
        };
        var corTexto = nivel == NivelRiscoApr.Critico ? Colors.White : Colors.Black;
        return container => container.Background(cor).Padding(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1)
            .DefaultTextStyle(t => t.FontColor(corTexto));
    }

    private static string RotuloNivelRisco(NivelRiscoApr nivel) => nivel switch
    {
        NivelRiscoApr.Baixo => "BAIXO",
        NivelRiscoApr.Moderado => "MODERADO",
        NivelRiscoApr.Alto => "ALTO",
        NivelRiscoApr.Critico => "CRÍTICO",
        _ => nivel.ToString(),
    };
}
