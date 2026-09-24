using System.Net;
using System.Text.Json;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Assinatura;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

// A foto de cadastro é a régua de todo reconhecimento posterior: uma referência ruim derruba a
// confiança de todas as assinaturas depois, e quem está no canteiro não tem como saber que o
// problema está no cadastro. Decisão do usuário em 24/09: recusar a foto na hora do cadastro até
// ela ficar boa, em vez de aceitar qualquer imagem.
public class CadastroFacialQualidadeTests
{
    static CadastroFacialQualidadeTests()
    {
        CpfCriptografiaContexto.Configurar(
            chaveCriptografia: Enumerable.Repeat((byte)1, 32).ToArray(),
            chaveHash: Enumerable.Repeat((byte)2, 32).ToArray());
    }

    private static byte[] Jpeg(int largura, int altura)
    {
        using var imagem = new Image<Rgba32>(largura, altura);
        using var memoria = new MemoryStream();
        imagem.Save(memoria, new JpegEncoder());
        return memoria.ToArray();
    }

    private static SstDbContext CriarDb(string nome)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static IOptions<AssinaturaOptions> Opcoes() => Options.Create(new AssinaturaOptions
    {
        AzureFaceApiEndpoint = "https://fake.cognitiveservices.azure.com",
        AzureFaceApiKey = "chave-fake",
    });

    private static HttpResponseMessage Json(object corpo) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(corpo)) };

    private static object RostoDetectado(int lado, string qualidade) => new
    {
        faceRectangle = new { width = lado, height = lado, top = 0, left = 0 },
        faceAttributes = new { qualityForRecognition = qualidade },
    };

    private static async Task<(SstDbContext db, Trabalhador trabalhador)> SemearAsync(string nomeBanco)
    {
        var db = CriarDb(nomeBanco);
        var obra = new Obra
        {
            Codigo = "OB1",
            Nome = "Obra Teste",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
            AzureFacePersonGroupId = "obra-x",
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            FuncaoId = Guid.NewGuid(),
            Nome = "Gabriel dos Santos Brito",
            Cpf = "00000000000",
            // O cadastro facial exige os dois consentimentos antes de qualquer coisa.
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow,
            ConsentimentoBiometriaEm = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();
        return (db, trabalhador);
    }

    // Falha barata primeiro: imagem pequena nem chega a gastar chamada no Azure.
    [Fact]
    public async Task CadastrarAsync_ImagemDeResolucaoBaixa_RecusaSemChamarOAzure()
    {
        var (db, trabalhador) = await SemearAsync(nameof(CadastrarAsync_ImagemDeResolucaoBaixa_RecusaSemChamarOAzure));
        var factory = new HttpClientFactoryFalso(_ => throw new InvalidOperationException("não deveria chamar a rede"));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CadastrarAsync(trabalhador.Id, Jpeg(320, 240), default));

        Assert.Contains("resolução baixa", ex.Message);
    }

    [Fact]
    public async Task CadastrarAsync_NenhumRostoNaFoto_Recusa()
    {
        var (db, trabalhador) = await SemearAsync(nameof(CadastrarAsync_NenhumRostoNaFoto_Recusa));
        var factory = new HttpClientFactoryFalso(req =>
            req.RequestUri!.AbsolutePath.EndsWith("/detect")
                ? Json(new List<object>())
                : throw new InvalidOperationException("não deveria passar do detect"));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CadastrarAsync(trabalhador.Id, Jpeg(800, 800), default));

        Assert.Contains("Nenhum rosto", ex.Message);
    }

    [Fact]
    public async Task CadastrarAsync_MaisDeUmRosto_Recusa()
    {
        var (db, trabalhador) = await SemearAsync(nameof(CadastrarAsync_MaisDeUmRosto_Recusa));
        var factory = new HttpClientFactoryFalso(req =>
            req.RequestUri!.AbsolutePath.EndsWith("/detect")
                ? Json(new[] { RostoDetectado(300, "high"), RostoDetectado(280, "high") })
                : throw new InvalidOperationException("não deveria passar do detect"));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CadastrarAsync(trabalhador.Id, Jpeg(800, 800), default));

        Assert.Contains("Mais de um rosto", ex.Message);
    }

    // Imagem grande, mas a pessoa longe da câmera: o rosto em si fica pequeno demais para servir de
    // referência, mesmo o Azure dizendo que a qualidade é alta.
    [Fact]
    public async Task CadastrarAsync_RostoPequenoNaFoto_Recusa()
    {
        var (db, trabalhador) = await SemearAsync(nameof(CadastrarAsync_RostoPequenoNaFoto_Recusa));
        var factory = new HttpClientFactoryFalso(req =>
            req.RequestUri!.AbsolutePath.EndsWith("/detect")
                ? Json(new[] { RostoDetectado(120, "high") })
                : throw new InvalidOperationException("não deveria passar do detect"));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CadastrarAsync(trabalhador.Id, Jpeg(1200, 1200), default));

        Assert.Contains("pequeno demais", ex.Message);
    }

    [Theory]
    [InlineData("low", "baixa")]
    [InlineData("medium", "média")]
    public async Task CadastrarAsync_QualidadeInsuficiente_RecusaDizendoONivel(string nivelAzure, string nivelTraduzido)
    {
        var (db, trabalhador) = await SemearAsync($"qualidade-{nivelAzure}");
        var factory = new HttpClientFactoryFalso(req =>
            req.RequestUri!.AbsolutePath.EndsWith("/detect")
                ? Json(new[] { RostoDetectado(400, nivelAzure) })
                : throw new InvalidOperationException("não deveria passar do detect"));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CadastrarAsync(trabalhador.Id, Jpeg(800, 800), default));

        Assert.Contains("não está boa o suficiente", ex.Message);
        Assert.Contains(nivelTraduzido, ex.Message);
        // A mensagem precisa ensinar o que corrigir, não só reprovar.
        Assert.Contains("iluminação", ex.Message);
    }

    // Foto boa segue o fluxo normal: detect de qualidade, cadastro do Person, adição da face e
    // treino do grupo.
    [Fact]
    public async Task CadastrarAsync_FotoDeQualidadeAlta_ProssegueECadastraAFace()
    {
        var (db, trabalhador) = await SemearAsync(nameof(CadastrarAsync_FotoDeQualidadeAlta_ProssegueECadastraAFace));
        var adicionouFace = false;
        var factory = new HttpClientFactoryFalso(req =>
        {
            var caminho = req.RequestUri!.AbsolutePath;
            if (caminho.EndsWith("/detect")) return Json(new[] { RostoDetectado(400, "high") });
            if (caminho.EndsWith("/persistedFaces"))
            {
                adicionouFace = true;
                return Json(new { persistedFaceId = Guid.NewGuid().ToString() });
            }
            if (caminho.EndsWith("/train")) return new HttpResponseMessage(HttpStatusCode.Accepted);
            if (caminho.EndsWith("/training")) return Json(new { status = "succeeded" });
            if (caminho.EndsWith("/persons")) return Json(new { personId = Guid.NewGuid().ToString() });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        await servico.CadastrarAsync(trabalhador.Id, Jpeg(800, 800), default);

        Assert.True(adicionouFace);
        Assert.NotNull((await db.Trabalhadores.SingleAsync(t => t.Id == trabalhador.Id)).AzureFacePersonId);
    }
}
