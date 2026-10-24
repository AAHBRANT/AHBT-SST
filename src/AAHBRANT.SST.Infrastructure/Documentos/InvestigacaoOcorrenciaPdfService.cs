using System.Globalization;
using AAHBRANT.SST.Application.Acidentes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Relatório de Investigação de Ocorrência — layout aprovado pelo usuário em 23/10
// (docs/mockups/relatorio-investigacao-ocorrencia-v1.html): seções numeradas com faixa na cor da
// marca, campos em grade, plano de ação em tabela, fotos 3 por linha e duas linhas de assinatura
// em branco (assinatura à mão no impresso). Cabeçalho e rodapé padrão de todos os documentos.
public class InvestigacaoOcorrenciaPdfService : IInvestigacaoOcorrenciaPdfService
{
    private const string CorMarca = "#670000";
    private const string CorLinha = "#CFC9C4";
    private const string CorRotulo = "#5C5652";
    private const string CorFaixa = "#F4F1F0";
    private const string CorConcluida = "#E3EFE3";
    private const string CorAberta = "#EBE9AD";
    private const string CorVencida = "#F6D5D5";
    private static readonly CultureInfo PtBr = new("pt-BR");

    public byte[] Gerar(InvestigacaoOcorrenciaPdfModelo modelo)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(1.5f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(9));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "Relatório de Investigação de Ocorrência", modelo.ObraNome, modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(8).Column(coluna =>
                {
                    coluna.Spacing(10);

                    coluna.Item().AlignCenter().Text(LinhaResumo(modelo)).FontSize(8).SemiBold().FontColor(CorRotulo);

                    coluna.Item().Element(c => Secao(c, "1. Identificação", corpo => Grade(corpo, new[]
                    {
                        ("Tipo", modelo.Tipo, 1),
                        ("Data / hora", DataHora(modelo.Data, modelo.Hora), 1),
                        ("Local", Valor(modelo.Local), 2),
                        ("Atividade", Valor(modelo.AtividadeNome), 2),
                        (modelo.Envolvidos.Count > 1 ? $"Envolvidos ({modelo.Envolvidos.Count})" : "Envolvido",
                            modelo.Envolvidos.Count > 0 ? string.Join("; ", modelo.Envolvidos) : "Não informado", 2),
                    })));

                    coluna.Item().Element(c => Secao(c, "2. Descrição da ocorrência", corpo => Texto(corpo, modelo.Descricao)));

                    coluna.Item().Element(c => Secao(c, "3. Consequências", corpo => Grade(corpo, new[]
                    {
                        ("Gravidade", modelo.Gravidade, 1),
                        ("Afastamento", modelo.HouveAfastamento ? (modelo.DiasAfastamento is { } d ? $"Sim — {d} dia(s)" : "Sim") : "Não", 1),
                        ("Nº da CAT", Valor(modelo.NumeroCat, "—"), 1),
                        ("Dias debitados", modelo.DiasDebitados.ToString(PtBr), 1),
                        ("Lesão", Valor(modelo.Lesao, "Não houve"), 2),
                        ("Atendimento", Valor(modelo.Atendimento, "Não houve"), 2),
                        ("Consequência", Valor(modelo.Consequencia), 4),
                    })));

                    coluna.Item().Element(c => Secao(c, "4. Análise e causas", corpo => corpo.Column(analise =>
                    {
                        analise.Item().Element(g => Grade(g, new[] { ("Metodologia", Valor(modelo.Metodologia, "Não definida"), 4) }));
                        analise.Item().Element(t => Texto(t, Valor(modelo.Causas, "Causas ainda não registradas.")));
                    })));

                    coluna.Item().Element(c => Secao(c, "5. Plano de ação", corpo => PlanoAcao(corpo, modelo.Acoes)));

                    if (modelo.Reuniao is not null)
                        coluna.Item().Element(c => Secao(c, "6. Reunião de análise", corpo => Reuniao(corpo, modelo.Reuniao)));

                    // Numeração segue sem buraco quando não houve reunião.
                    var n = modelo.Reuniao is null ? 6 : 7;

                    // EnsureSpace: o título da seção não fica sozinho no pé da página, longe da 1ª linha de fotos.
                    if (modelo.Fotos.Count > 0)
                        coluna.Item().EnsureSpace(190).Element(c => Secao(c, $"{n++}. Registro fotográfico", corpo => Fotos(corpo, modelo.Fotos)));

                    coluna.Item().ShowEntire().Element(c => Secao(c, $"{n}. Responsáveis pela investigação", Assinaturas));
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Investigação de ocorrência", null, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
            });
        });

        return documento.GeneratePdf();
    }

    private static string LinhaResumo(InvestigacaoOcorrenciaPdfModelo modelo)
    {
        var situacao = modelo.DataConclusaoInvestigacao is { } concluida
            ? $"{modelo.Situacao} em {concluida:dd/MM/yyyy}"
            : modelo.Situacao;
        return $"Tipo: {modelo.Tipo}   ·   Data: {DataHora(modelo.Data, modelo.Hora)}   ·   Situação: {situacao}";
    }

    private static string DataHora(DateTime data, TimeSpan? hora) =>
        hora is { } h ? $"{data:dd/MM/yyyy} · {h:hh\\:mm}" : data.ToString("dd/MM/yyyy");

    private static string Valor(string? texto, string vazio = "Não informado") =>
        string.IsNullOrWhiteSpace(texto) ? vazio : texto.Trim();

    private static void Secao(IContainer container, string titulo, Action<IContainer> corpo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Background(CorMarca).PaddingVertical(3).PaddingHorizontal(6)
                .Text(titulo.ToUpper(PtBr)).FontSize(9).Bold().FontColor(Colors.White).LetterSpacing(0.04f);
            coluna.Item().Element(corpo);
        });
    }

    // Grade de 4 colunas com rótulo miúdo em cima e valor em negrito — cada campo ocupa 1, 2 ou 4.
    private static void Grade(IContainer container, IReadOnlyList<(string Rotulo, string Valor, int Colunas)> campos)
    {
        container.Table(tabela =>
        {
            tabela.ColumnsDefinition(c =>
            {
                c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn();
            });

            foreach (var (rotulo, valor, colunas) in campos)
            {
                tabela.Cell().ColumnSpan((uint)colunas).Border(0.5f).BorderColor(CorLinha).PaddingVertical(3).PaddingHorizontal(6).Column(celula =>
                {
                    celula.Item().Text(rotulo.ToUpper(PtBr)).FontSize(6.5f).FontColor(CorRotulo).LetterSpacing(0.04f);
                    celula.Item().Text(valor).SemiBold();
                });
            }
        });
    }

    private static void Texto(IContainer container, string texto)
    {
        container.Border(0.5f).BorderColor(CorLinha).Padding(6).Text(texto).Justify();
    }

    private static void Reuniao(IContainer container, InvestigacaoOcorrenciaPdfReuniaoModelo reuniao)
    {
        if (reuniao.RealizadaEm is { } realizada)
        {
            container.Column(c =>
            {
                c.Item().Element(g => Grade(g, new[]
                {
                    ("Realizada em", $"{realizada:dd/MM/yyyy} · {realizada:HH:mm}", 2),
                    ("Presentes", reuniao.Presentes is { Count: > 0 } p ? string.Join("; ", p) : "—", 2),
                }));
                c.Item().Element(t => Texto(t, Valor(reuniao.Ata, "Ata não registrada.")));
            });
            return;
        }

        var onde = reuniao.NoTeams ? "Teams" : "fora do Teams";
        Grade(container, new[]
        {
            ("Agendada para", $"{reuniao.Inicio:dd/MM/yyyy} · {reuniao.Inicio:HH:mm}–{reuniao.Fim:HH:mm} ({onde})", 2),
            ("Convidados", reuniao.Participantes.Count > 0 ? string.Join("; ", reuniao.Participantes) : "—", 2),
        });
    }

    private static void PlanoAcao(IContainer container, IReadOnlyList<InvestigacaoOcorrenciaPdfAcaoModelo> acoes)
    {
        if (acoes.Count == 0)
        {
            Texto(container, "Nenhuma ação registrada no plano.");
            return;
        }

        container.Border(0.5f).BorderColor(CorLinha).Table(tabela =>
        {
            tabela.ColumnsDefinition(c =>
            {
                c.ConstantColumn(18);
                c.RelativeColumn(4);
                c.RelativeColumn(1.3f);
                c.RelativeColumn(2);
                c.RelativeColumn(1.3f);
                c.RelativeColumn(1.4f);
            });

            tabela.Header(cabecalho =>
            {
                foreach (var titulo in new[] { "#", "Ação", "Tipo", "Responsável", "Prazo", "Situação" })
                    cabecalho.Cell().Background(CorFaixa).BorderBottom(0.5f).BorderColor(CorLinha).Padding(4)
                        .Text(titulo.ToUpper(PtBr)).FontSize(7).SemiBold().FontColor(CorRotulo);
            });

            var ordem = 1;
            foreach (var acao in acoes)
            {
                var corSituacao = acao.Concluida ? CorConcluida : acao.Situacao == "Vencida" ? CorVencida : CorAberta;

                Celula(tabela).Text((ordem++).ToString(PtBr));
                Celula(tabela).Column(c =>
                {
                    c.Item().Text(acao.Descricao);
                    if (!string.IsNullOrWhiteSpace(acao.Fundamentacao))
                        c.Item().Text($"Fundamentação: {acao.Fundamentacao}").FontSize(7).FontColor(CorRotulo);
                });
                Celula(tabela).Text(acao.Tipo);
                Celula(tabela).Text(Valor(acao.ResponsavelNome, "—"));
                Celula(tabela).Text(acao.Prazo?.ToString("dd/MM/yyyy") ?? "—");
                Celula(tabela).AlignLeft().Background(corSituacao).PaddingHorizontal(4).PaddingVertical(1)
                    .Text(acao.Situacao).FontSize(7.5f).Bold();
            }
        });
    }

    private static IContainer Celula(TableDescriptor tabela) =>
        tabela.Cell().BorderBottom(0.5f).BorderColor(CorLinha).Padding(4);

    private static void Fotos(IContainer container, IReadOnlyList<InvestigacaoOcorrenciaPdfFotoModelo> fotos)
    {
        container.Border(0.5f).BorderColor(CorLinha).Padding(6).Table(tabela =>
        {
            tabela.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });

            var numero = 1;
            foreach (var foto in fotos)
            {
                var legenda = LegendaFoto(numero++, foto);
                tabela.Cell().Padding(3).ShowEntire().Column(c =>
                {
                    c.Item().AspectRatio(4f / 3f).Border(0.5f).BorderColor(CorLinha).Image(RecortarParaPreencherSlot(foto.Conteudo)).FitArea();
                    c.Item().PaddingTop(2).Text(legenda).FontSize(6.5f).FontColor(CorRotulo);
                });
            }
        });
    }

    private static string LegendaFoto(int numero, InvestigacaoOcorrenciaPdfFotoModelo foto)
    {
        var partes = new List<string> { $"Foto {numero}" };
        if (foto.CapturadaEmUtc is { } capturada)
            partes.Add(HorarioBrasilia.De(capturada).ToString("dd/MM/yyyy HH:mm"));
        if (foto.Latitude is { } lat && foto.Longitude is { } lon)
            partes.Add($"{lat.ToString("0.00000", CultureInfo.InvariantCulture)}, {lon.ToString("0.00000", CultureInfo.InvariantCulture)}");
        return string.Join(" · ", partes);
    }

    private static void Assinaturas(IContainer container)
    {
        container.Border(0.5f).BorderColor(CorLinha).PaddingTop(36).PaddingBottom(10).PaddingHorizontal(18).Row(linha =>
        {
            linha.RelativeItem().Element(c => LinhaAssinatura(c, "Técnico de Segurança do Trabalho", "Nome · registro"));
            linha.ConstantItem(28);
            linha.RelativeItem().Element(c => LinhaAssinatura(c, "Engenheiro de Segurança / Responsável da obra", "Nome · CREA"));
        });
    }

    private static void LinhaAssinatura(IContainer container, string cargo, string identificacao)
    {
        container.BorderTop(0.75f).BorderColor(Colors.Black).PaddingTop(3).Column(c =>
        {
            c.Item().AlignCenter().Text(cargo).FontSize(8.5f);
            c.Item().AlignCenter().Text(identificacao).FontSize(7).FontColor(CorRotulo);
        });
    }

    // Mesmo recorte central de InspecaoPdfService (object-fit: cover), na proporção 4:3 do slot.
    private static byte[] RecortarParaPreencherSlot(byte[] foto)
    {
        using var imagem = AAHBRANT.SST.Infrastructure.Imagens.ImagemSegura.Carregar(foto);
        imagem.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(640, 480),
            Mode = ResizeMode.Crop,
        }));

        using var saida = new MemoryStream();
        imagem.Save(saida, new JpegEncoder { Quality = 85 });
        return saida.ToArray();
    }
}
