using System.Net;
using System.Text.Json;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Assinatura;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

// Fake de IHttpClientFactory que roteia por caminho da URL — simula as respostas do Azure Face API
// sem chamada real de rede, permitindo testar os thresholds e a lógica de detect→identify.
public class HttpClientFactoryFalso : IHttpClientFactory
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    public HttpClientFactoryFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    private class HandlerFalso : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_responder(request));
    }

    public HttpClient CreateClient(string name = "") => new(new HandlerFalso(_responder));
}

public class AzureFaceAutenticacaoStrategyTests
{
    static AzureFaceAutenticacaoStrategyTests()
    {
        CpfCriptografiaContexto.Configurar(
            chaveCriptografia: Enumerable.Repeat((byte)1, 32).ToArray(),
            chaveHash: Enumerable.Repeat((byte)2, 32).ToArray());
    }

    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static IOptions<AssinaturaOptions> Opcoes() => Options.Create(new AssinaturaOptions
    {
        AzureFaceApiEndpoint = "https://fake.cognitiveservices.azure.com",
        AzureFaceApiKey = "chave-fake",
        LimiarConfiancaFacial = 0.85,
        LimiarConfiancaFacialMinimo = 0.60,
    });

    private static HttpResponseMessage Json(object corpo, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(corpo)) };

    [Fact]
    public async Task IdentificarAsync_ObraSemFacialHabilitado_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(IdentificarAsync_ObraSemFacialHabilitado_LancaInvalidOperationException));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste", MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.Nenhum };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(_ => throw new InvalidOperationException("não deveria chamar a rede"));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        await Assert.ThrowsAsync<InvalidOperationException>(() => servico.IdentificarAsync(obra.Id, new byte[] { 1 }, default));
    }

    [Fact]
    public async Task IdentificarAsync_NenhumRostoDetectado_RetornaMotivoNenhumRosto()
    {
        var db = CriarDb(nameof(IdentificarAsync_NenhumRostoDetectado_RetornaMotivoNenhumRosto));
        var obra = new Obra
        {
            Codigo = "OB1", Nome = "Obra Teste",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
            AzureFacePersonGroupId = "obra-x",
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(req =>
            req.RequestUri!.AbsolutePath.EndsWith("/detect") ? Json(new List<object>()) : throw new InvalidOperationException("chamada inesperada"));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var resultado = await servico.IdentificarAsync(obra.Id, new byte[] { 1 }, default);

        Assert.False(resultado.Aceito);
        Assert.Equal(MotivoRejeicaoFacial.NenhumRostoDetectado, resultado.Motivo);
    }

    [Fact]
    public async Task IdentificarAsync_ConfiancaAbaixoDoLimiarMinimo_RetornaRostoNaoReconhecido()
    {
        var db = CriarDb(nameof(IdentificarAsync_ConfiancaAbaixoDoLimiarMinimo_RetornaRostoNaoReconhecido));
        var obra = new Obra
        {
            Codigo = "OB1", Nome = "Obra Teste",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
            AzureFacePersonGroupId = "obra-x",
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/detect"))
                return Json(new[] { new { faceId = "face-1" } });
            if (req.RequestUri!.AbsolutePath.EndsWith("/identify"))
                return Json(new[] { new { faceId = "face-1", candidates = new[] { new { personId = "person-1", confidence = 0.40 } } } });
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var resultado = await servico.IdentificarAsync(obra.Id, new byte[] { 1 }, default);

        Assert.False(resultado.Aceito);
        Assert.Equal(MotivoRejeicaoFacial.RostoNaoReconhecido, resultado.Motivo);
    }

    [Fact]
    public async Task IdentificarAsync_ConfiancaEntreLimiares_RetornaConfiancaBaixa()
    {
        var db = CriarDb(nameof(IdentificarAsync_ConfiancaEntreLimiares_RetornaConfiancaBaixa));
        var obra = new Obra
        {
            Codigo = "OB1", Nome = "Obra Teste",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
            AzureFacePersonGroupId = "obra-x",
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/detect"))
                return Json(new[] { new { faceId = "face-1" } });
            if (req.RequestUri!.AbsolutePath.EndsWith("/identify"))
                return Json(new[] { new { faceId = "face-1", candidates = new[] { new { personId = "person-1", confidence = 0.70 } } } });
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var resultado = await servico.IdentificarAsync(obra.Id, new byte[] { 1 }, default);

        Assert.False(resultado.Aceito);
        Assert.Equal(MotivoRejeicaoFacial.ConfiancaBaixa, resultado.Motivo);
        Assert.Equal(0.70, resultado.Confianca);
    }

    [Fact]
    public async Task IdentificarAsync_ConfiancaAltaETrabalhadorEncontrado_Aceita()
    {
        var db = CriarDb(nameof(IdentificarAsync_ConfiancaAltaETrabalhadorEncontrado_Aceita));
        var obra = new Obra
        {
            Codigo = "OB1", Nome = "Obra Teste",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
            AzureFacePersonGroupId = "obra-x",
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow,
            AzureFacePersonId = "person-1",
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/detect"))
                return Json(new[] { new { faceId = "face-1" } });
            if (req.RequestUri!.AbsolutePath.EndsWith("/identify"))
                return Json(new[] { new { faceId = "face-1", candidates = new[] { new { personId = "person-1", confidence = 0.95 } } } });
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var resultado = await servico.IdentificarAsync(obra.Id, new byte[] { 1 }, default);

        Assert.True(resultado.Aceito);
        Assert.Equal(trabalhador.Id, resultado.Resultado!.TrabalhadorId);
        Assert.Equal(MetodoAutenticacaoAssinatura.ReconhecimentoFacial, resultado.Resultado.Metodo);
    }

    [Fact]
    public async Task CadastrarAsync_TrabalhadorSemConsentimento_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(CadastrarAsync_TrabalhadorSemConsentimento_LancaInvalidOperationException));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(_ => throw new InvalidOperationException("não deveria chamar a rede"));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        await Assert.ThrowsAsync<InvalidOperationException>(() => servico.CadastrarAsync(trabalhador.Id, new byte[] { 1 }, default));
    }

    [Fact]
    public async Task CadastrarAsync_PrimeiroCadastroDaObra_CriaGrupoPessoaEPersisteAzureFacePersonId()
    {
        var db = CriarDb(nameof(CadastrarAsync_PrimeiroCadastroDaObra_CriaGrupoPessoaEPersisteAzureFacePersonId));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow,
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow, ConsentimentoBiometriaEm = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var chamadasTreino = 0;
        var factory = new HttpClientFactoryFalso(req =>
        {
            var caminho = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Put && caminho.Contains("/persongroups/"))
                return new HttpResponseMessage(HttpStatusCode.OK);
            if (caminho.EndsWith("/persons"))
                return Json(new { personId = "person-novo" });
            if (caminho.EndsWith("/persistedFaces"))
                return Json(new { persistedFaceId = "face-persistida-1" });
            if (caminho.EndsWith("/train"))
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            if (caminho.EndsWith("/training"))
            {
                chamadasTreino++;
                return Json(new { status = "succeeded" });
            }
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        await servico.CadastrarAsync(trabalhador.Id, new byte[] { 1, 2, 3 }, default);

        var obraAtualizada = await db.Obras.FirstAsync(o => o.Id == obra.Id);
        var trabalhadorAtualizado = await db.Trabalhadores.FirstAsync(t => t.Id == trabalhador.Id);
        Assert.NotNull(obraAtualizada.AzureFacePersonGroupId);
        Assert.Equal("person-novo", trabalhadorAtualizado.AzureFacePersonId);
        Assert.True(chamadasTreino >= 1);
    }
}
