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
    private const string CorLinha = GradeDocumentoPadrao.CorLinha;

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
                    // 05/11: assinatura do instrutor no fim, no bloco padrão (selo vinho + linha + nome).
                    if (modelo.Instrutor is { } instrutor)
                        coluna.Item().PaddingTop(14).AlignCenter().Width(260).Element(c => AssinaturaPdfPadrao.Bloco(
                            c, "Instrutor", instrutor.Nome, instrutor.CargoRegistro, instrutor.AssinadoEm, instrutor.Metodo, instrutor.Hash));
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Lista de Presença de Treinamento", modelo.NumeroCertificado, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
            });

            // 05/11: certificado de assinaturas no fim (evidências de cada participante).
            if (modelo.Certificado is { } certificado)
                CertificadoAssinaturasPdf.Adicionar(container, certificado);
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

        GradeDocumentoPadrao.Desenhar(container, new GradeDocumentoPadrao.Campo[]
        {
            new("Curso", curso, 4, Destaque: true),
            new("Data de realização", modelo.DataRealizacao.ToString("dd/MM/yyyy"), 1),
            new("Carga horária", $"{modelo.CargaHorariaRealizada} h", 1),
            new("Nº do certificado", modelo.NumeroCertificado, 2),
            new("Instrutor / instituição", modelo.InstituicaoInstrutor, 2),
            new("Status", status, 2),
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
                    colunas.ConstantColumn(20);
                    colunas.RelativeColumn(2.6f);
                    colunas.RelativeColumn(2.2f);
                    colunas.RelativeColumn(0.9f);
                    colunas.RelativeColumn(3.2f);
                });

                tabela.Header(cabecalho =>
                {
                    CelulaCabecalho(cabecalho.Cell(), "Nº");
                    CelulaCabecalho(cabecalho.Cell(), "Nome");
                    CelulaCabecalho(cabecalho.Cell(), "Função · CBO");
                    CelulaCabecalho(cabecalho.Cell(), "Matrícula");
                    CelulaCabecalho(cabecalho.Cell(), "Assinatura");
                });

                var numero = 0;
                foreach (var participante in modelo.Participantes)
                {
                    numero++;
                    Celula(tabela.Cell(), numero.ToString());
                    Celula(tabela.Cell(), participante.TrabalhadorNome);
                    Celula(tabela.Cell(), participante.FuncaoComCbo ?? "—");
                    Celula(tabela.Cell(), participante.TrabalhadorMatricula ?? string.Empty);
                    // 05/11: selo na linha (AssinaturaPdfPadrao.SeloLinha); sem presença = "Ausente".
                    tabela.Cell().Border(0.5f).BorderColor(CorLinha).Padding(3).AlignMiddle()
                        .Element(c => AssinaturaPdfPadrao.SeloLinha(c, participante.PresencaConfirmadaEm, participante.Metodo, participante.Selo, "Ausente"));
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
