using System.Globalization;
using AAHBRANT.SST.Application.Treinamentos.LeituraCertificado;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;
using QuestPDF.Fluent;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using UglyToad.PdfPig;

namespace AAHBRANT.SST.Infrastructure.LeituraIa;

// Leitura de certificado de treinamento pelo Claude (07/11/2026): orientação da imagem + dados do certificado.
public class ClaudeLeitorCertificado : ILeitorCertificadoIa
{
    private const string Instrucoes = """
        Você lê certificados de treinamento de segurança do trabalho (NRs) de obras no Brasil.
        1) Diga quantos graus (sentido horário) é preciso girar a imagem para o texto ficar em pé e legível:
           0 (já está em pé), 90, 180 (de cabeça para baixo) ou 270.
        2) Extraia os dados exatamente como impressos. O que não estiver no documento fica nulo — nunca invente.
           - curso: nome do curso; se houver lista de cursos do catálogo, devolva o nome IGUAL ao da lista que corresponde.
           - norma: a NR citada (ex.: "NR-35").
           - dataRealizacao: data de conclusão/realização no formato AAAA-MM-DD.
           - cargaHoraria: horas totais (número inteiro).
           - instituicao, instrutor, registroInstrutor (CREA/MTE), numeroCertificado, nomeParticipante.
        """;

    private record Lido(int Rotacao, string? Curso, string? Norma, string? DataRealizacao, int? CargaHoraria, string? Instituicao,
        string? Instrutor, string? RegistroInstrutor, string? NumeroCertificado, string? NomeParticipante);

    private static readonly object TextoOuNulo = new { type = new[] { "string", "null" } };
    private static readonly object Schema = new
    {
        type = "object",
        properties = new
        {
            rotacao = new { type = "integer", @enum = new[] { 0, 90, 180, 270 } },
            curso = TextoOuNulo,
            norma = TextoOuNulo,
            dataRealizacao = TextoOuNulo,
            cargaHoraria = new { type = new[] { "integer", "null" } },
            instituicao = TextoOuNulo,
            instrutor = TextoOuNulo,
            registroInstrutor = TextoOuNulo,
            numeroCertificado = TextoOuNulo,
            nomeParticipante = TextoOuNulo,
        },
        required = new[] { "rotacao", "curso", "norma", "dataRealizacao", "cargaHoraria", "instituicao", "instrutor", "registroInstrutor", "numeroCertificado", "nomeParticipante" },
        additionalProperties = false,
    };

    private readonly ClaudeChatJsonCliente _chat;
    public ClaudeLeitorCertificado(ClaudeChatJsonCliente chat) => _chat = chat;

    public async Task<CertificadoLidoIa> LerAsync(byte[]? imagemJpeg, string? textoPdf, IReadOnlyList<string> cursos, CancellationToken ct)
    {
        var msg = new System.Text.StringBuilder();
        if (cursos.Count > 0)
        {
            msg.AppendLine("Cursos do catálogo:");
            foreach (var c in cursos) msg.AppendLine($"- {c}");
        }
        IReadOnlyList<string>? imagens = null;
        if (imagemJpeg is not null)
        {
            imagens = new[] { $"data:image/jpeg;base64,{Convert.ToBase64String(ImagemPaginaPdf.Reduzir(imagemJpeg))}" };
            msg.AppendLine("O certificado é a imagem anexa.");
        }
        else
        {
            msg.AppendLine("Texto do certificado (PDF digital, já em pé):");
            msg.AppendLine(textoPdf!.Length > 8000 ? textoPdf[..8000] : textoPdf);
        }

        var l = await _chat.ObterAsync<Lido>(Instrucoes, msg.ToString(), "certificado_treinamento", Schema, ct,
            maxTokens: 800, timeoutSegundos: 90, imagens: imagens);
        DateTime? data = DateTime.TryParseExact(l.DataRealizacao, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        return new CertificadoLidoIa(imagemJpeg is null ? 0 : l.Rotacao, l.Curso, l.Norma, data, l.CargaHoraria, l.Instituicao, l.Instrutor,
            l.RegistroInstrutor, l.NumeroCertificado, l.NomeParticipante);
    }
}

public class ProcessadorArquivoCertificado : IProcessadorArquivoCertificado
{
    public byte[]? ImagemParaLeitura(byte[] conteudo, string contentType)
    {
        if (contentType is "image/jpeg" or "image/png")
        {
            using var img = SixLabors.ImageSharp.Image.Load(conteudo);
            img.Mutate(x => x.AutoOrient());
            using var saida = new MemoryStream();
            img.SaveAsJpeg(saida, new JpegEncoder { Quality = 85 });
            return saida.ToArray();
        }
        if (contentType == "application/pdf")
        {
            var jpegs = ImagemPaginaPdf.ExtrairJpegs(conteudo, new[] { 0 });
            return jpegs.TryGetValue(0, out var j) ? j : null;
        }
        return null;
    }

    public string? TextoPdf(byte[] conteudo, string contentType)
    {
        if (contentType != "application/pdf") return null;
        using var doc = PdfDocument.Open(conteudo);
        return string.Join("\n", doc.GetPages().Take(2).Select(p => p.Text));
    }

    public (byte[] Conteudo, string ContentType) Girar(byte[] conteudo, string contentType, int graus)
    {
        if (graus is not (90 or 180 or 270)) return (conteudo, contentType);
        if (contentType is "image/jpeg" or "image/png")
            return (GirarImagem(conteudo, graus, contentType == "image/png"), contentType);

        // PDF digitalizado: refaz o PDF com as imagens das páginas já giradas. PDF sem imagem (digital) fica como está.
        using var doc = PdfDocument.Open(conteudo);
        var total = doc.NumberOfPages;
        var imagens = ImagemPaginaPdf.ExtrairJpegs(conteudo, Enumerable.Range(0, total));
        if (imagens.Count < total) return (conteudo, contentType);
        var giradas = Enumerable.Range(0, total).Select(i => GirarImagem(imagens[i], graus, false)).ToList();
        var pdf = Document.Create(c =>
        {
            foreach (var img in giradas)
                c.Page(p =>
                {
                    p.Size(QuestPDF.Helpers.PageSizes.A4);
                    p.Margin(0);
                    p.Content().Image(img).FitArea();
                });
        }).GeneratePdf();
        return (pdf, contentType);
    }

    private static byte[] GirarImagem(byte[] conteudo, int graus, bool png)
    {
        using var img = SixLabors.ImageSharp.Image.Load(conteudo);
        img.Mutate(x => x.AutoOrient().Rotate(graus));
        using var saida = new MemoryStream();
        if (png) img.Save(saida, new PngEncoder());
        else img.Save(saida, new JpegEncoder { Quality = 90 });
        return saida.ToArray();
    }
}
