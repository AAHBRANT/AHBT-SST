using AAHBRANT.SST.Application.PermissoesTrabalho;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Reproduz o layout do formulário "PT – PERMISSÃO DE TRABALHO | REV.01" (planilha do usuário,
// 2026-08-29): cabeçalho, pré-requisitos (§2), tipos de trabalho especiais (§3), verificações
// pré-início C/NC/NA (§4, com NC destacado em vermelho — "nenhuma atividade poderá iniciar com
// item crítico NC"), EPIs/EPCs (§5), riscos críticos (§6), assinaturas de liberação (§7),
// suspensão/encerramento (§8) e ciência da equipe executante (§9), com a "REGRA DE OURO" fixa.
// Retrato (A4), pois o formulário original é predominantemente vertical.
public class PtPdfService : IPtPdfService
{
    private const string CorMarca = "#670000";
    private const string CorNaoConforme = "#C00000";
    private const string CorConforme = "#A9D18E";
    private const string CorNaoAplicavel = "#D9D9D9";

    private const string TextoRegraOuro =
        "REGRA DE OURO: nenhuma atividade poderá ter início com item crítico de verificação NÃO CONFORME ou " +
        "pré-requisito não atendido. Em caso de dúvida, PARE e consulte a liderança/SST antes de prosseguir.";

    public byte[] Gerar(PtPdfModelo modelo)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(1.2f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(8));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "PT – PERMISSÃO DE TRABALHO | REV.01", modelo.ObraNome, modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(10).Column(coluna =>
                {
                    coluna.Spacing(8);

                    coluna.Item().Element(c => SecaoCabecalho(c, modelo));
                    coluna.Item().Element(c => SecaoPreRequisitos(c, modelo));
                    coluna.Item().Element(c => SecaoTiposTrabalho(c, modelo));
                    coluna.Item().Element(c => SecaoVerificacoes(c, modelo));
                    coluna.Item().Element(c => SecaoEpisEpcs(c, modelo));
                    coluna.Item().Element(c => SecaoRiscosCriticos(c, modelo));
                    coluna.Item().Text(TextoRegraOuro).FontSize(7).Bold().FontColor(CorNaoConforme);
                    coluna.Item().Element(c => SecaoAssinaturasLiberacao(c, modelo));
                    coluna.Item().Element(c => SecaoStatus(c, modelo));
                    coluna.Item().Element(c => SecaoEnvolvidos(c, modelo));
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "PT", modelo.NumeroPt, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
            });
        });

        return documento.GeneratePdf();
    }

    private static void SecaoCabecalho(IContainer container, PtPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem(2).Text(t => { t.Span("OBRA / CONTRATO: ").SemiBold(); t.Span(modelo.ObraNome ?? "não informado"); });
                linha.RelativeItem().Text(t => { t.Span("Nº PT: ").SemiBold(); t.Span(modelo.NumeroPt ?? "-"); });
                linha.RelativeItem().Text(t => { t.Span("DATA: ").SemiBold(); t.Span(modelo.Data.ToString("dd/MM/yyyy")); });
            });
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem(2).Text(t => { t.Span("ATIVIDADE: ").SemiBold(); t.Span(modelo.DescricaoAtividade); });
                linha.RelativeItem().Text(t => { t.Span("LOCAL: ").SemiBold(); t.Span(modelo.Local); });
            });
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem(2).Text(t => { t.Span("EMPRESA EXECUTANTE: ").SemiBold(); t.Span(modelo.EmpresaExecutante ?? "-"); });
                linha.RelativeItem().Text(t => { t.Span("HORÁRIO: ").SemiBold(); t.Span($"{FormatarHora(modelo.HorarioInicio)} às {FormatarHora(modelo.HorarioFim)}"); });
            });
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem(2).Text(t => { t.Span("RESP. EXECUÇÃO: ").SemiBold(); t.Span(modelo.ResponsavelExecucaoNome ?? "-"); });
                linha.RelativeItem().Text(t => { t.Span("RESP. ÁREA: ").SemiBold(); t.Span(modelo.ResponsavelAreaNome ?? "-"); });
                linha.RelativeItem().Text(t => { t.Span("VALIDADE: ").SemiBold(); t.Span(modelo.Validade.HasValue ? modelo.Validade.Value.ToString("dd/MM/yyyy") : "-"); });
            });
        });
    }

    private static void SecaoPreRequisitos(IContainer container, PtPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Background(CorMarca).Padding(3).Text("2. PRÉ-REQUISITOS").FontColor(Colors.White).Bold().FontSize(9);
            foreach (var item in modelo.PreRequisitos)
            {
                coluna.Item().Row(linha =>
                {
                    linha.ConstantItem(16).Text(item.Marcado ? "[X]" : "[ ]").Bold();
                    linha.RelativeItem().Text(item.Rotulo);
                });
            }
        });
    }

    private static void SecaoTiposTrabalho(IContainer container, PtPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Background(CorMarca).Padding(3).Text("3. TIPOS DE TRABALHO ESPECIAIS").FontColor(Colors.White).Bold().FontSize(9);
            if (modelo.TiposTrabalho.Count == 0)
            {
                coluna.Item().Text("Nenhum tipo de trabalho especial selecionado.").Italic();
                return;
            }
            foreach (var tipo in modelo.TiposTrabalho)
            {
                coluna.Item().Text(tipo.DescricaoOutro is { Length: > 0 } ? $"• {tipo.Rotulo}: {tipo.DescricaoOutro}" : $"• {tipo.Rotulo}");
            }
        });
    }

    private static void SecaoVerificacoes(IContainer container, PtPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Background(CorMarca).Padding(3).Text("4. VERIFICAÇÕES PRÉ-INÍCIO").FontColor(Colors.White).Bold().FontSize(9);
            coluna.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(70);
                });
                table.Header(header =>
                {
                    header.Cell().Element(CabecalhoCelula).Text("ITEM");
                    header.Cell().Element(CabecalhoCelula).AlignCenter().Text("RESPOSTA");
                });
                foreach (var verificacao in modelo.Verificacoes)
                {
                    table.Cell().Element(Celula).Text(verificacao.Rotulo);
                    table.Cell().Element(CelulaResposta(verificacao.Resposta)).AlignCenter().Text(RotuloResposta(verificacao.Resposta)).Bold();
                }
            });
        });
    }

    private static void SecaoEpisEpcs(IContainer container, PtPdfModelo modelo)
    {
        container.Row(linha =>
        {
            linha.RelativeItem().Column(coluna =>
            {
                coluna.Item().Background(CorMarca).Padding(3).Text("5. EPIs").FontColor(Colors.White).Bold().FontSize(9);
                foreach (var epi in modelo.Epis)
                    coluna.Item().Text(epi.Complemento is { Length: > 0 } ? $"• {epi.Rotulo} ({epi.Complemento})" : $"• {epi.Rotulo}");
                if (modelo.OutrosEpis is { Length: > 0 })
                    coluna.Item().Text($"Outros: {modelo.OutrosEpis}");
            });

            linha.ConstantItem(16);

            linha.RelativeItem().Column(coluna =>
            {
                coluna.Item().Background(CorMarca).Padding(3).Text("EPCs").FontColor(Colors.White).Bold().FontSize(9);
                foreach (var epc in modelo.Epcs)
                    coluna.Item().Text($"• {epc}");
                if (modelo.OutrosEpcs is { Length: > 0 })
                    coluna.Item().Text($"Outros: {modelo.OutrosEpcs}");
            });
        });
    }

    private static void SecaoRiscosCriticos(IContainer container, PtPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Background(CorMarca).Padding(3).Text("6. RISCOS CRÍTICOS / CONTROLES COMPLEMENTARES").FontColor(Colors.White).Bold().FontSize(9);
            if (modelo.RiscosCriticos.Count == 0)
            {
                coluna.Item().Text("Nenhum risco crítico registrado.").Italic();
                return;
            }
            coluna.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1.2f);
                });
                table.Header(header =>
                {
                    header.Cell().Element(CabecalhoCelula).Text("RISCO / CONDIÇÃO");
                    header.Cell().Element(CabecalhoCelula).Text("CONTROLE COMPLEMENTAR");
                    header.Cell().Element(CabecalhoCelula).Text("RESPONSÁVEL / EVIDÊNCIA");
                });
                foreach (var risco in modelo.RiscosCriticos)
                {
                    table.Cell().Element(Celula).Text(risco.RiscoCondicao);
                    table.Cell().Element(Celula).Text(risco.ControleComplementar ?? "-");
                    table.Cell().Element(Celula).Text(risco.ResponsavelEvidencia ?? "-");
                }
            });
        });
    }

    private static void SecaoAssinaturasLiberacao(IContainer container, PtPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Background(CorMarca).Padding(3).Text("7. LIBERAÇÃO").FontColor(Colors.White).Bold().FontSize(9);
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem().Element(c => BlocoAssinatura(c, "EMITENTE / RESPONSÁVEL PELA ÁREA", modelo.Emitente));
                linha.ConstantItem(16);
                linha.RelativeItem().Element(c => BlocoAssinatura(c, "RESPONSÁVEL PELA EXECUÇÃO", modelo.Execucao));
                if (modelo.Sst is not null)
                {
                    linha.ConstantItem(16);
                    linha.RelativeItem().Element(c => BlocoAssinatura(c, "SST (QUANDO REQUERIDO)", modelo.Sst));
                }
            });
        });
    }

    private static void BlocoAssinatura(IContainer container, string titulo, PtPdfAssinatura assinatura)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(coluna =>
        {
            coluna.Item().Text(titulo).Bold().FontSize(7.5f);
            coluna.Item().PaddingTop(4).Text($"Nome: {assinatura.Nome ?? "____________________________"}");
            coluna.Item().Text("Assinatura: ______________________");
            coluna.Item().Text($"Data: {(assinatura.Data.HasValue ? assinatura.Data.Value.ToString("dd/MM/yyyy HH:mm") : "____/____/______")}");
        });
    }

    private static void SecaoStatus(IContainer container, PtPdfModelo modelo)
    {
        if (modelo.Suspensao is null && modelo.Encerramento is null) return;

        container.Column(coluna =>
        {
            coluna.Item().Background(CorMarca).Padding(3).Text("8. SUSPENSÃO / ENCERRAMENTO").FontColor(Colors.White).Bold().FontSize(9);
            if (modelo.Suspensao is { } suspensao)
                coluna.Item().Text($"Suspensa por {suspensao.Nome ?? "-"} em {(suspensao.Data.HasValue ? suspensao.Data.Value.ToString("dd/MM/yyyy HH:mm") : "-")}. Motivo: {suspensao.Motivo ?? "-"}");
            if (modelo.Encerramento is { } encerramento)
                coluna.Item().Text($"Encerrada por {encerramento.Nome ?? "-"} em {(encerramento.Data.HasValue ? encerramento.Data.Value.ToString("dd/MM/yyyy HH:mm") : "-")}. Observações: {encerramento.Observacoes ?? "-"}");
        });
    }

    private static void SecaoEnvolvidos(IContainer container, PtPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Background(CorMarca).Padding(3).Text("9. CIÊNCIA DA EQUIPE EXECUTANTE").FontColor(Colors.White).Bold().FontSize(9);

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

    private static string FormatarHora(TimeSpan? horario) => horario.HasValue ? horario.Value.ToString(@"hh\:mm") : "-";

    private static IContainer CabecalhoCelula(IContainer container) =>
        container.Background(Colors.Grey.Lighten3).Padding(3).BorderBottom(1).BorderColor(CorMarca).DefaultTextStyle(t => t.FontSize(6.5f).Bold());

    private static IContainer Celula(IContainer container) =>
        container.Padding(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).DefaultTextStyle(t => t.FontSize(7));

    private static Func<IContainer, IContainer> CelulaResposta(RespostaVerificacaoPt? resposta)
    {
        var cor = resposta switch
        {
            RespostaVerificacaoPt.Conforme => CorConforme,
            RespostaVerificacaoPt.NaoConforme => CorNaoConforme,
            RespostaVerificacaoPt.NaoAplicavel => CorNaoAplicavel,
            _ => "#FFFFFF",
        };
        var corTexto = resposta == RespostaVerificacaoPt.NaoConforme ? Colors.White : Colors.Black;
        return container => container.Background(cor).Padding(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1)
            .DefaultTextStyle(t => t.FontColor(corTexto));
    }

    private static string RotuloResposta(RespostaVerificacaoPt? resposta) => resposta switch
    {
        RespostaVerificacaoPt.Conforme => "C",
        RespostaVerificacaoPt.NaoConforme => "NC",
        RespostaVerificacaoPt.NaoAplicavel => "N/A",
        _ => "-",
    };
}
