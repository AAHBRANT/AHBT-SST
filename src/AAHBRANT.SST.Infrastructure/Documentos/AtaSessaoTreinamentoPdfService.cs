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
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "Ata de Treinamento — Anexo de Evidências", modelo.ObraNome, modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(12).Column(coluna =>
                {
                    coluna.Spacing(10);
                    coluna.Item().Element(c => DadosDaTurma(c, modelo));
                    coluna.Item().Element(c => TabelaPresenca(c, modelo));
                    if (modelo.Fotos.Count > 0)
                        coluna.Item().Element(c => GradeDeFotos(c, modelo.Fotos));
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Ata de Sessão de Treinamento", modelo.NumeroCertificado, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
            });
        });

        return documento.GeneratePdf();
    }

    private static void DadosDaTurma(IContainer container, AtaSessaoTreinamentoPdfModelo modelo)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(coluna =>
        {
            coluna.Spacing(3);
            coluna.Item().Text(t =>
            {
                t.Span("Curso: ").SemiBold();
                t.Span(modelo.CursoNome).Bold().FontColor(CorMarca);
                if (!string.IsNullOrWhiteSpace(modelo.NormaReferencia))
                    t.Span($" ({modelo.NormaReferencia})");
            });
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem().Text(t => { t.Span("Data de realização: ").SemiBold(); t.Span(modelo.DataRealizacao.ToString("dd/MM/yyyy")); });
                linha.RelativeItem().Text(t => { t.Span("Carga horária: ").SemiBold(); t.Span($"{modelo.CargaHorariaRealizada}h"); });
            });
            if (!string.IsNullOrWhiteSpace(modelo.InstituicaoInstrutor))
                coluna.Item().Text(t => { t.Span("Instrutor / instituição: ").SemiBold(); t.Span(modelo.InstituicaoInstrutor); });
            if (!string.IsNullOrWhiteSpace(modelo.NumeroCertificado))
                coluna.Item().Text(t => { t.Span("Nº do certificado: ").SemiBold(); t.Span(modelo.NumeroCertificado); });
            coluna.Item().Text(t =>
            {
                t.Span("Status: ").SemiBold();
                t.Span(modelo.DataEncerramento is not null
                    ? $"Concluída em {modelo.DataEncerramento:dd/MM/yyyy HH:mm}"
                    : "Em andamento");
            });
        });
    }

    private static void TabelaPresenca(IContainer container, AtaSessaoTreinamentoPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Text($"Lista de presença ({modelo.Participantes.Count(p => p.PresencaConfirmadaEm is not null)}/{modelo.Participantes.Count} confirmadas)")
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
                    CelulaCabecalho(cabecalho.Cell(), "Presença (biometria)");
                });

                foreach (var participante in modelo.Participantes)
                {
                    Celula(tabela.Cell(), participante.TrabalhadorNome);
                    Celula(tabela.Cell(), participante.TrabalhadorMatricula ?? string.Empty);
                    Celula(tabela.Cell(), participante.PresencaConfirmadaEm is not null
                        ? $"Confirmada às {participante.PresencaConfirmadaEm:dd/MM/yyyy HH:mm}"
                        : "Ausente");
                }
            });
        });
    }

    private static void CelulaCabecalho(IContainer container, string texto) =>
        container.Background(CorMarca).Padding(4).Text(texto).FontSize(9).Bold().FontColor(Colors.White);

    private static void Celula(IContainer container, string texto) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(texto).FontSize(9);

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
        using var imagem = SixLabors.ImageSharp.Image.Load(foto);
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
