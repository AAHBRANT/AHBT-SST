using AAHBRANT.SST.Application.Cipa;
using AAHBRANT.SST.Application.EstoquesEpi.Reposicao;
using AAHBRANT.SST.Application.PermissoesTrabalho;
using AAHBRANT.SST.Infrastructure.Documentos;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Grade padrão dos dados de identificação (05/11): os documentos que passaram a usá-la continuam
// gerando o PDF. Com GRADE_PDF_AMOSTRA definida (pasta), salva cópias para conferência visual.
public class GradeDocumentoPadraoTests
{
    static GradeDocumentoPadraoTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public void Pt_GeraPdf() => Salvar("pt.pdf", new PtPdfService().Gerar(new PtPdfModelo(
        "PT-0012", "Ponte Rio Cuiá", null, "Montagem de forma no pilar P3", "Eixo 4 — pilar P3", "Construtora X",
        new DateTime(2026, 11, 5), new TimeSpan(7, 0, 0), new TimeSpan(17, 0, 0), new DateTime(2026, 11, 5),
        "Gilton Pereira Miranda", "Ana Almeida Pereira",
        new List<PtPdfItemBinario>(), new List<PtPdfTipoTrabalho>(), new List<PtPdfVerificacao>(), new List<PtPdfEpi>(),
        null, new List<string>(), null, new List<PtPdfRiscoCritico>(),
        new PtPdfAssinatura("Gilton", null), new PtPdfAssinatura("Ana", null), null, null, null,
        new List<PtPdfEnvolvido>(), "hash", "https://exemplo/validar", Png(), false)));

    [Fact]
    public void AtaEleicaoCipa_GeraPdf() => Salvar("cipa-eleicao.pdf", new CipaPdfService().GerarAtaEleicao(new AtaEleicaoCipaPdfModelo(
        "Ponte Rio Cuiá", null, "CIPA-2026-0001", new DateTime(2026, 9, 1), new DateTime(2026, 10, 1), new DateTime(2026, 10, 1, 18, 0, 0),
        new[] { new AtaEleicaoCipaCandidatoModelo("Ana Almeida", "AUR-0101", 12, "Eleito") }, "hash", "https://exemplo/validar", Png())));

    [Fact]
    public void AtaReuniaoCipa_GeraPdf() => Salvar("cipa-reuniao.pdf", new CipaPdfService().GerarAtaReuniao(new AtaReuniaoCipaPdfModelo(
        "Ponte Rio Cuiá", null, "Reunião ordinária", new DateTime(2026, 10, 15), "Análise dos acidentes do mês", "Reforçar DDS de altura",
        new[] { new AtaReuniaoCipaParticipanteModelo("Ana Almeida", true) }, "hash", "https://exemplo/validar", Png())));

    [Fact]
    public void SolicitacaoReposicao_GeraPdf() => Salvar("reposicao.pdf", new SolicitacaoReposicaoEpiPdfService().Gerar(new SolicitacaoReposicaoEpiPdfModelo(
        "Ponte Rio Cuiá", null, "SRE-2026-0003", new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc), "Gilton Pereira Miranda", "Técnico de Segurança",
        30, "Urgente para a concretagem", new[] { new SolicitacaoReposicaoEpiItemPdfModelo("Luva de vaqueta", "12345", 4, 0, 20, 36, "G") },
        null, null, "hash", "https://exemplo/validar", Png())));

    private static void Salvar(string nome, byte[] pdf)
    {
        Assert.True(pdf.Length > 0);
        var pasta = Environment.GetEnvironmentVariable("GRADE_PDF_AMOSTRA");
        if (!string.IsNullOrEmpty(pasta)) File.WriteAllBytes(Path.Combine(pasta, nome), pdf);
    }

    private static byte[] Png()
    {
        using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(4, 4);
        using var ms = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(img, ms);
        return ms.ToArray();
    }
}
