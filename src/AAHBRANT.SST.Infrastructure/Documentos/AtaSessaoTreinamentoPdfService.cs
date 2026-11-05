using AAHBRANT.SST.Application.SessoesTreinamento;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Ata/Anexo de Evidências da turma (item 5 da proposta do usuário, 04/09) — documento consolidado
// para auditoria interna/externa: dados da turma, lista de presença biométrica com horários e as
// fotos de evidência. Mesmo padrão visual AAHBRANT (#670000) e mesmo cabeçalho compartilhado de
// DdsPdfService/DdsSemanalPdfService.
public class AtaSessaoTreinamentoPdfService : IAtaSessaoTreinamentoPdfService
{
    private const string CorMarca = "#670000";
    private const string CorLinha = "#CFC9C4";
    private const string CorRotulo = "#5C5652";

    public byte[] Gerar(AtaSessaoTreinamentoPdfModelo modelo)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(10));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "Lista de Presença de Treinamento", modelo.ObraNome, modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(12).Column(coluna =>
                {
                    coluna.Spacing(10);
                    coluna.Item().Element(c => DadosDaTurma(c, modelo));
                    coluna.Item().Element(c => TabelaPresenca(c, modelo));
                    if (modelo.Fotos.Count > 0)
                        coluna.Item().Element(c => GradeDeFotos(c, modelo.Fotos));
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Lista de Presença de Treinamento", modelo.NumeroCertificado, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
            });
        });

        return documento.GeneratePdf();
    }

    // Pedido do usuário (05/11): dados da turma em grade, no mesmo padrão da Investigação de
    // Ocorrência — 4 colunas, rótulo miúdo em cima e valor embaixo, cada campo numa célula com borda.
    private static void DadosDaTurma(IContainer container, AtaSessaoTreinamentoPdfModelo modelo)
    {
        var curso = string.IsNullOrWhiteSpace(modelo.NormaReferencia)
            ? modelo.CursoNome
            : $"{modelo.CursoNome} ({modelo.NormaReferencia})";
        var status = modelo.DataEncerramento is not null
            ? $"Concluída em {modelo.DataEncerramento:dd/MM/yyyy HH:mm}"
            : "Em andamento";

        container.Table(tabela =>
        {
            tabela.ColumnsDefinition(c =>
            {
                c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn();
            });

            CampoGrade(tabela, "Curso", curso, 4, destaque: true);
            CampoGrade(tabela, "Data de realização", modelo.DataRealizacao.ToString("dd/MM/yyyy"), 1);
            CampoGrade(tabela, "Carga horária", $"{modelo.CargaHorariaRealizada} h", 1);
            CampoGrade(tabela, "Nº do certificado", Valor(modelo.NumeroCertificado), 2);
            CampoGrade(tabela, "Instrutor / instituição", Valor(modelo.InstituicaoInstrutor), 2);
            CampoGrade(tabela, "Status", status, 2);
        });
    }

    private static string Valor(string? texto) => string.IsNullOrWhiteSpace(texto) ? "—" : texto;

    private static void CampoGrade(TableDescriptor tabela, string rotulo, string valor, int colunas, bool destaque = false)
    {
        tabela.Cell().ColumnSpan((uint)colunas).Border(0.5f).BorderColor(CorLinha).PaddingVertical(3).PaddingHorizontal(6).Column(celula =>
        {
            celula.Item().Text(rotulo.ToUpperInvariant()).FontSize(6.5f).FontColor(CorRotulo).LetterSpacing(0.04f);
            var texto = celula.Item().Text(valor).SemiBold();
            if (destaque) texto.FontColor(CorMarca);
        });
    }

    private static void TabelaPresenca(IContainer container, AtaSessaoTreinamentoPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Text($"Participantes ({modelo.Participantes.Count(p => p.PresencaConfirmadaEm is not null)}/{modelo.Participantes.Count} presenças confirmadas)")
                .FontSize(12).Bold().FontColor(CorMarca);

            coluna.Item().PaddingTop(4).Table(tabela =>
            {
                tabela.ColumnsDefinition(colunas =>
                {
                    colunas.RelativeColumn(3);
                    colunas.RelativeColumn(1);
                    colunas.RelativeColumn(2);
                });

                tabela.Header(cabecalho =>
                {
                    CelulaCabecalho(cabecalho.Cell(), "Nome");
                    CelulaCabecalho(cabecalho.Cell(), "Matrícula");
                    CelulaCabecalho(cabecalho.Cell(), "Assinatura");
                });

                foreach (var participante in modelo.Participantes)
                {
                    Celula(tabela.Cell(), participante.TrabalhadorNome);
                    Celula(tabela.Cell(), participante.TrabalhadorMatricula ?? string.Empty);
                    // Padrão único de assinatura (AssinaturaPdfPadrao); ausente = sem assinatura.
                    tabela.Cell().Border(0.5f).BorderColor(CorLinha).Padding(4).AlignMiddle().Column(assinatura =>
                    {
                        if (participante.PresencaConfirmadaEm is { } quando)
                            AssinaturaPdfPadrao.Legenda(assinatura, quando, participante.Metodo);
                        else
                            assinatura.Item().AlignCenter().Text("Ausente").FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                    });
                }
            });
        });
    }

    private static void CelulaCabecalho(IContainer container, string texto) =>
        container.Border(0.5f).BorderColor(CorMarca).Background(CorMarca).Padding(4).Text(texto).FontSize(9).Bold().FontColor(Colors.White);

    private static void Celula(IContainer container, string texto) =>
        container.Border(0.5f).BorderColor(CorLinha).Padding(4).AlignMiddle().Text(texto).FontSize(9);

    private static void GradeDeFotos(IContainer container, IReadOnlyList<byte[]> fotos)
    {
        container.Column(coluna =>
        {
            coluna.Item().Text("Evidências fotográficas da turma").FontSize(12).Bold().FontColor(CorMarca);
            coluna.Item().PaddingTop(4).Row(linha =>
            {
                foreach (var foto in fotos)
                {
                    linha.RelativeItem().Padding(2).Height(140).Border(1).BorderColor(Colors.Grey.Lighten2)
                        .Image(RecortarParaPreencherSlot(foto)).FitArea();
                }
            });
        });
    }

    // Mesma técnica de InspecaoPdfService.RecortarParaPreencherSlot — recorta pelo centro na
    // proporção do slot antes de desenhar (object-fit: cover), evitando faixas vazias quando a
    // proporção da foto original não bate com a do slot.
    private static byte[] RecortarParaPreencherSlot(byte[] foto)
    {
        using var imagem = AAHBRANT.SST.Infrastructure.Imagens.ImagemSegura.Carregar(foto);
        imagem.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(500, 380),
            Mode = ResizeMode.Crop,
        }));

        using var saida = new MemoryStream();
        imagem.Save(saida, new JpegEncoder { Quality = 85 });
        return saida.ToArray();
    }
}
