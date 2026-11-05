using AAHBRANT.SST.Application.Trabalhadores;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Trabalhadores;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Foto facial 3×4 nos PDFs (04/11): fotos antigas deitadas são cortadas no centro só para exibição.
public class FotoRetrato34Tests
{
    static FotoRetrato34Tests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static byte[] Jpeg(int largura, int altura)
    {
        using var img = new Image<Rgba32>(largura, altura, new Rgba32(120, 140, 160));
        using var ms = new MemoryStream();
        img.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Cortar_FotoDeitada_SaiEm3x4()
    {
        using var img = Image.Load(FotoRetrato34.Cortar(Jpeg(640, 480)));
        Assert.Equal(480, img.Height);
        Assert.Equal(360, img.Width);
    }

    [Fact]
    public void Cortar_FotoJaEmPe_NaoMuda()
    {
        var original = Jpeg(360, 480);
        Assert.Same(original, FotoRetrato34.Cortar(original));
    }

    [Fact]
    public void Cortar_BytesInvalidos_VoltaComoEsta()
    {
        var lixo = new byte[] { 1, 2, 3 };
        Assert.Same(lixo, FotoRetrato34.Cortar(lixo));
    }

    [Fact]
    public void RelatorioFiscalizacao_ComFotosDeitadas_GeraPdf()
    {
        var fotoReal = Environment.GetEnvironmentVariable("SST_FOTO_TESTE");
        var foto = string.IsNullOrWhiteSpace(fotoReal) ? Jpeg(640, 480) : File.ReadAllBytes(fotoReal);
        var assinatura = new AssinaturaPerfilDto(Guid.NewGuid(), Guid.NewGuid(), "EntregaEpi", Guid.NewGuid(),
            MetodoAutenticacaoAssinatura.ReconhecimentoFacial, new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc), "10.0.0.1",
            true, true, "ABC123", foto, "image/jpeg");
        var imagemDigital = Environment.GetEnvironmentVariable("SST_DIGITAL_TESTE") is { Length: > 0 } d ? File.ReadAllBytes(d) : Jpeg(320, 480);
        var digital = assinatura with { SignatarioId = Guid.NewGuid(), Metodo = MetodoAutenticacaoAssinatura.Biometria, FotoEvidenciaConteudo = imagemDigital, FotoEvidenciaHash = "DEF456" };
        var semCadastro = assinatura with { SignatarioId = Guid.NewGuid() };
        var referencias = new Dictionary<Guid, byte[]> { [assinatura.SignatarioId] = foto, [digital.SignatarioId] = imagemDigital };
        var perfil = new PerfilCompletoTrabalhadorDto(
            Guid.NewGuid(), "Trabalhador de Teste", "30", "000.000.000-00", null,
            Guid.NewGuid(), "OBRA TESTE", Guid.NewGuid(), "Ajudante Geral",
            TipoVinculo.Clt, new DateTime(2026, 7, 1), false, false, null, "APTO",
            new List<AAHBRANT.SST.Application.Asos.AsoDto>(), new List<AAHBRANT.SST.Application.EntregasEpi.EntregaEpiDto>(),
            new List<FrequenciaTrocaEpiDto>(), new List<AAHBRANT.SST.Application.Treinamentos.TreinamentoDto>(),
            new AssiduidadeDdsDto(0, 0), new List<RiscoExpostoDto>(), new List<OcorrenciaDto>(),
            new List<AssinaturaPerfilDto> { assinatura, digital, semCadastro }, 0, new List<MotivoTrocaEpiDto>(), null, null, null, null);

        var pdf = new RelatorioFiscalizacaoPdfService().Gerar(perfil, new IdentificacaoRelatorioFiscalizacao(foto, "Cliente", "00.000.000/0001-00", referencias));

        Assert.True(pdf.Length > 1000);
        var destino = Environment.GetEnvironmentVariable("SST_PDF_TESTE_DESTINO");
        if (!string.IsNullOrWhiteSpace(destino)) File.WriteAllBytes(destino, pdf);
    }
}
