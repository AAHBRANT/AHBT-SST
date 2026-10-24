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
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Yield();
            if (request.Content is not null)
                _ = await request.Content.ReadAsByteArrayAsync(ct);
            return _responder(request);
        }
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

    // O cadastro passou a recusar foto ruim antes de tocar no Azure (24/09): a validação começa
    // pela resolução da imagem, então os testes de cadastro precisam de um JPEG de verdade, com
    // tamanho suficiente — `new byte[] { 1 }` não é imagem e para na primeira linha da validação.
    private static byte[] JpegDeCadastro()
    {
        using var imagem = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(800, 800);
        using var memoria = new MemoryStream();
        imagem.Save(memoria, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
        return memoria.ToArray();
    }

    // Resposta do detect de qualidade usado só no cadastro — rosto único, grande e nítido.
    private static object RostoBomParaCadastro() => new
    {
        faceRectangle = new { width = 400, height = 400, top = 0, left = 0 },
        faceAttributes = new { qualityForRecognition = "high" },
    };

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
            AzureFacePersonId = "person-1", TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow, ConsentimentoBiometriaEm = DateTime.UtcNow,
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

    // Rastro da validação para o log de assinaturas: modelo real do grupo (lido do Azure, não o
    // recognition_04 da checagem de qualidade), grupo da obra e id da requisição do Azure.
    [Fact]
    public async Task IdentificarAsync_Aceita_GuardaModeloDoGrupoGrupoEIdDaRequisicao()
    {
        var db = CriarDb(nameof(IdentificarAsync_Aceita_GuardaModeloDoGrupoGrupoEIdDaRequisicao));
        var obra = new Obra
        {
            Codigo = "OB1", Nome = "Obra Teste",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
            AzureFacePersonGroupId = "obra-rastro",
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        db.Trabalhadores.Add(new Trabalhador
        {
            ObraId = obra.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow, AzureFacePersonId = "person-1",
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow, ConsentimentoBiometriaEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(req =>
        {
            var caminho = req.RequestUri!.AbsolutePath;
            if (caminho.EndsWith("/detect"))
                return Json(new[] { new { faceId = "face-1" } });
            if (caminho.EndsWith("/identify"))
            {
                var identificacao = Json(new[] { new { faceId = "face-1", candidates = new[] { new { personId = "person-1", confidence = 0.95 } } } });
                identificacao.Headers.Add("apim-request-id", "7f3c91ae-0000-4b1f-9c2d-aaaaaaaab204");
                return identificacao;
            }
            if (caminho.EndsWith("/persongroups/obra-rastro"))
                return Json(new { personGroupId = "obra-rastro", recognitionModel = "recognition_03" });
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        var resultado = await servico.IdentificarAsync(obra.Id, new byte[] { 1 }, default);

        Assert.True(resultado.Aceito);
        Assert.Equal("recognition_03", resultado.Resultado!.ValidacaoModelo);
        Assert.Equal("obra-rastro", resultado.Resultado.ValidacaoGrupoId);
        Assert.Equal("7f3c91ae-0000-4b1f-9c2d-aaaaaaaab204", resultado.Resultado.ValidacaoRequisicaoId);
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

        await Assert.ThrowsAsync<InvalidOperationException>(() => servico.CadastrarAsync(trabalhador.Id, JpegDeCadastro(), default));
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
            if (caminho.EndsWith("/detect"))
                return Json(new[] { RostoBomParaCadastro() });
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

        await servico.CadastrarAsync(trabalhador.Id, JpegDeCadastro(), default);

        var obraAtualizada = await db.Obras.FirstAsync(o => o.Id == obra.Id);
        var trabalhadorAtualizado = await db.Trabalhadores.FirstAsync(t => t.Id == trabalhador.Id);
        Assert.NotNull(obraAtualizada.AzureFacePersonGroupId);
        Assert.Equal("person-novo", trabalhadorAtualizado.AzureFacePersonId);
        Assert.True(chamadasTreino >= 1);
    }

    [Fact]
    public async Task CadastrarAsync_Concluido_SubstituiAFotoDoPerfilPelaFotoDoCadastro()
    {
        var db = CriarDb(nameof(CadastrarAsync_Concluido_SubstituiAFotoDoPerfilPelaFotoDoCadastro));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        var fotoAntiga = new byte[] { 1, 2, 3 };
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow,
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow, ConsentimentoBiometriaEm = DateTime.UtcNow,
            FotoConteudo = fotoAntiga, FotoContentType = "image/png",
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(req =>
        {
            var caminho = req.RequestUri!.AbsolutePath;
            if (caminho.EndsWith("/detect")) return Json(new[] { RostoBomParaCadastro() });
            if (req.Method == HttpMethod.Put && caminho.Contains("/persongroups/")) return new HttpResponseMessage(HttpStatusCode.OK);
            if (caminho.EndsWith("/persons")) return Json(new { personId = "person-novo" });
            if (caminho.EndsWith("/persistedFaces")) return Json(new { persistedFaceId = "face-1" });
            if (caminho.EndsWith("/train")) return new HttpResponseMessage(HttpStatusCode.Accepted);
            if (caminho.EndsWith("/training")) return Json(new { status = "succeeded" });
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());
        var foto = JpegDeCadastro();

        await servico.CadastrarAsync(trabalhador.Id, foto, default);

        var atualizado = await db.Trabalhadores.FirstAsync(t => t.Id == trabalhador.Id);
        Assert.Equal(foto, atualizado.FotoConteudo);
        Assert.Equal("image/jpeg", atualizado.FotoContentType);
        var guardada = await db.FotosCadastroFacial.SingleAsync(f => f.TrabalhadorId == trabalhador.Id);
        Assert.Equal(foto, guardada.Conteudo);
    }

    [Fact]
    public async Task CadastrarAsync_ObraComGrupoPersistido_GarantePersonGroupAntesDeAdicionarFace()
    {
        var db = CriarDb(nameof(CadastrarAsync_ObraComGrupoPersistido_GarantePersonGroupAntesDeAdicionarFace));
        var obra = new Obra
        {
            Codigo = "OB1",
            Nome = "Obra Teste",
            AzureFacePersonGroupId = "obra-antiga",
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            Nome = "Fulano",
            Cpf = "12345678901",
            DataAdmissao = DateTime.UtcNow,
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow,
            ConsentimentoBiometriaEm = DateTime.UtcNow,
            AzureFacePersonId = "person-existente",
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var garantiuGrupo = false;
        var factory = new HttpClientFactoryFalso(req =>
        {
            var caminho = req.RequestUri!.AbsolutePath;
            if (caminho.EndsWith("/detect"))
                return Json(new[] { RostoBomParaCadastro() });
            if (req.Method == HttpMethod.Put && caminho.EndsWith("/persongroups/obra-antiga"))
            {
                garantiuGrupo = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            if (caminho.EndsWith("/persistedFaces"))
            {
                Assert.True(garantiuGrupo);
                return Json(new { persistedFaceId = "face-persistida-1" });
            }
            if (caminho.EndsWith("/train"))
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            if (caminho.EndsWith("/training"))
                return Json(new { status = "succeeded" });
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        await servico.CadastrarAsync(trabalhador.Id, JpegDeCadastro(), default);

        Assert.True(garantiuGrupo);
    }

    [Fact]
    public async Task CadastrarAsync_PersonAntigoNaoExiste_RecriaPersonERetentaAdicionarFace()
    {
        var db = CriarDb(nameof(CadastrarAsync_PersonAntigoNaoExiste_RecriaPersonERetentaAdicionarFace));
        var obra = new Obra
        {
            Codigo = "OB1",
            Nome = "Obra Teste",
            AzureFacePersonGroupId = "obra-antiga",
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            Nome = "Fulano",
            Cpf = "12345678901",
            DataAdmissao = DateTime.UtcNow,
            TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow,
            ConsentimentoBiometriaEm = DateTime.UtcNow,
            AzureFacePersonId = "person-antigo",
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var tentativasFace = 0;
        var factory = new HttpClientFactoryFalso(req =>
        {
            var caminho = req.RequestUri!.AbsolutePath;
            if (caminho.EndsWith("/detect"))
                return Json(new[] { RostoBomParaCadastro() });
            if (req.Method == HttpMethod.Put && caminho.EndsWith("/persongroups/obra-antiga"))
                return new HttpResponseMessage(HttpStatusCode.OK);
            if (caminho.EndsWith("/persons"))
                return Json(new { personId = "person-recriado" });
            if (caminho.EndsWith("/persistedFaces"))
            {
                tentativasFace++;
                if (tentativasFace == 1)
                    return Json(new { error = new { code = "BadArgument", innererror = new { code = "PersonNotFound", message = "Person not found." } } }, HttpStatusCode.BadRequest);
                return Json(new { persistedFaceId = "face-persistida-1" });
            }
            if (caminho.EndsWith("/train"))
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            if (caminho.EndsWith("/training"))
                return Json(new { status = "succeeded" });
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        await servico.CadastrarAsync(trabalhador.Id, JpegDeCadastro(), default);

        var trabalhadorAtualizado = await db.Trabalhadores.FirstAsync(t => t.Id == trabalhador.Id);
        Assert.Equal(2, tentativasFace);
        Assert.Equal("person-recriado", trabalhadorAtualizado.AzureFacePersonId);
    }

    // Funcionário que muda de obra: o rosto precisa ir para o grupo da obra nova, senão ele nunca mais é
    // reconhecido (o Azure só procura no grupo da obra do documento).
    [Fact]
    public async Task RealocarCadastroAsync_TrocaDeObra_CopiaFotoParaGrupoNovoEApagaDoAntigo()
    {
        var db = CriarDb(nameof(RealocarCadastroAsync_TrocaDeObra_CopiaFotoParaGrupoNovoEApagaDoAntigo));
        var obraAntiga = new Obra { Codigo = "OB1", Nome = "Obra Antiga", AzureFacePersonGroupId = "grupo-antigo" };
        var obraNova = new Obra { Codigo = "OB2", Nome = "Obra Nova" };
        db.Obras.AddRange(obraAntiga, obraNova);
        await db.SaveChangesAsync();
        var trabalhador = new Trabalhador
        {
            ObraId = obraNova.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow,
            AzureFacePersonId = "person-antigo",
        };
        db.Trabalhadores.Add(trabalhador);
        db.FotosCadastroFacial.Add(new FotoCadastroFacial
        {
            TrabalhadorId = trabalhador.Id, Conteudo = new byte[] { 9, 9 }, ContentType = "image/jpeg",
            HashSha256 = "h", CapturadaEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var chamadas = new List<string>();
        var factory = new HttpClientFactoryFalso(req =>
        {
            var caminho = req.RequestUri!.AbsolutePath;
            chamadas.Add($"{req.Method} {caminho}");
            if (req.Method == HttpMethod.Put && caminho.Contains("/persongroups/"))
                return new HttpResponseMessage(HttpStatusCode.OK);
            if (caminho.EndsWith("/persons"))
                return Json(new { personId = "person-novo" });
            if (caminho.EndsWith("/persistedFaces"))
                return Json(new { persistedFaceId = "face-1" });
            if (req.Method == HttpMethod.Delete)
                return new HttpResponseMessage(HttpStatusCode.OK);
            if (caminho.EndsWith("/train"))
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            if (caminho.EndsWith("/training"))
                return Json(new { status = "succeeded" });
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        await servico.RealocarCadastroAsync(trabalhador.Id, obraAntiga.Id, default);

        var grupoNovo = $"obra-{obraNova.Id:N}";
        Assert.Equal("person-novo", (await db.Trabalhadores.FirstAsync(t => t.Id == trabalhador.Id)).AzureFacePersonId);
        Assert.Equal(grupoNovo, (await db.Obras.FirstAsync(o => o.Id == obraNova.Id)).AzureFacePersonGroupId);
        Assert.Contains($"POST /face/v1.0/persongroups/{grupoNovo}/persons/person-novo/persistedFaces", chamadas);
        Assert.Contains("DELETE /face/v1.0/persongroups/grupo-antigo/persons/person-antigo", chamadas);
    }

    [Fact]
    public async Task RealocarCadastroAsync_SemCadastroFacial_NaoChamaAzure()
    {
        var db = CriarDb(nameof(RealocarCadastroAsync_SemCadastroFacial_NaoChamaAzure));
        var obraAntiga = new Obra { Codigo = "OB1", Nome = "Obra Antiga" };
        var obraNova = new Obra { Codigo = "OB2", Nome = "Obra Nova" };
        db.Obras.AddRange(obraAntiga, obraNova);
        var trabalhador = new Trabalhador { ObraId = obraNova.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var factory = new HttpClientFactoryFalso(_ => throw new InvalidOperationException("não deveria chamar a rede"));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        await servico.RealocarCadastroAsync(trabalhador.Id, obraAntiga.Id, default);
    }

    private static async Task<(SstDbContext Db, Obra Obra, Trabalhador ComFoto, Trabalhador FotoRecusada)> CenarioMigracao(string nome)
    {
        var db = CriarDb(nome);
        var obra = new Obra { Codigo = "OB1", Nome = "Obra", AzureFacePersonGroupId = "obra-antiga" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        var comFoto = new Trabalhador { ObraId = obra.Id, Nome = "Osvanildo", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow, AzureFacePersonId = "p-antigo-1" };
        var recusada = new Trabalhador { ObraId = obra.Id, Nome = "Dailton", Cpf = "12345678902", DataAdmissao = DateTime.UtcNow, AzureFacePersonId = "p-antigo-2" };
        db.Trabalhadores.AddRange(comFoto, recusada);
        db.FotosCadastroFacial.AddRange(
            new FotoCadastroFacial { TrabalhadorId = comFoto.Id, Conteudo = new byte[] { 1 }, ContentType = "image/jpeg", HashSha256 = "a", CapturadaEm = DateTime.UtcNow },
            new FotoCadastroFacial { TrabalhadorId = recusada.Id, Conteudo = new byte[] { 2 }, ContentType = "image/jpeg", HashSha256 = "b", CapturadaEm = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return (db, obra, comFoto, recusada);
    }

    private static IOptions<AssinaturaOptions> OpcoesSemPausa()
    {
        var o = Opcoes();
        o.Value.IntervaloMigracaoFacialMs = 0;
        return o;
    }

    // Grupo antigo (recognition_01): recria no recognition_04 com as fotos guardadas, troca o grupo da obra e
    // o id de cada trabalhador. Quem teve a foto recusada fica sem cadastro, para poder refazer.
    [Fact]
    public async Task MigrarModeloReconhecimentoAsync_GrupoAntigo_RecriaNoModeloAtualEArquivaQuemFalhou()
    {
        var (db, obra, comFoto, recusada) = await CenarioMigracao(nameof(MigrarModeloReconhecimentoAsync_GrupoAntigo_RecriaNoModeloAtualEArquivaQuemFalhou));
        var grupoNovo = $"obra-{obra.Id:N}-r4";
        string? modeloCriado = null;
        var pessoas = 0;
        var apagados = new List<string>();
        var factory = new HttpClientFactoryFalso(req =>
        {
            var caminho = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Get && caminho.EndsWith("/persongroups/obra-antiga"))
                return Json(new { recognitionModel = "recognition_01" });
            if (req.Method == HttpMethod.Delete)
            {
                apagados.Add(caminho);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            if (req.Method == HttpMethod.Put && caminho.EndsWith($"/persongroups/{grupoNovo}"))
            {
                modeloCriado = JsonDocument.Parse(req.Content!.ReadAsStringAsync().Result).RootElement.GetProperty("recognitionModel").GetString();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            if (caminho.EndsWith("/persons"))
                return Json(new { personId = $"p-novo-{++pessoas}" });
            if (caminho.EndsWith("/persistedFaces"))
                return caminho.Contains("p-novo-1")
                    ? Json(new { persistedFaceId = "f" })
                    : Json(new { error = new { code = "InvalidImage", message = "No face detected" } }, HttpStatusCode.BadRequest);
            if (caminho.EndsWith("/train"))
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            if (caminho.EndsWith("/training"))
                return Json(new { status = "succeeded" });
            throw new InvalidOperationException("chamada inesperada: " + req.Method + " " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, OpcoesSemPausa());

        var resultado = await servico.MigrarModeloReconhecimentoAsync(obra.Id, default);

        Assert.NotNull(resultado);
        Assert.Equal(1, resultado!.Migrados);
        Assert.Equal("recognition_04", modeloCriado);
        Assert.Equal(grupoNovo, (await db.Obras.FirstAsync(o => o.Id == obra.Id)).AzureFacePersonGroupId);
        var t1 = await db.Trabalhadores.FirstAsync(t => t.Id == comFoto.Id);
        var t2 = await db.Trabalhadores.FirstAsync(t => t.Id == recusada.Id);
        Assert.NotNull(t1.AzureFacePersonId);
        Assert.StartsWith("p-novo-", t1.AzureFacePersonId);
        Assert.Null(t2.AzureFacePersonId);
        Assert.Equal(new[] { "Dailton" }, resultado.SemCadastroAposMigracao);
        Assert.False(await db.FotosCadastroFacial.AnyAsync(f => f.TrabalhadorId == recusada.Id));
        Assert.True(await db.FotosCadastroFacial.AnyAsync(f => f.TrabalhadorId == comFoto.Id));
        Assert.Contains("/face/v1.0/persongroups/obra-antiga", apagados);
    }

    [Fact]
    public async Task MigrarModeloReconhecimentoAsync_GrupoJaNoModeloAtual_NaoFazNada()
    {
        var (db, obra, _, _) = await CenarioMigracao(nameof(MigrarModeloReconhecimentoAsync_GrupoJaNoModeloAtual_NaoFazNada));
        var factory = new HttpClientFactoryFalso(req =>
            req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.EndsWith("/persongroups/obra-antiga")
                ? Json(new { recognitionModel = "recognition_04" })
                : throw new InvalidOperationException("não deveria chamar: " + req.RequestUri));
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, OpcoesSemPausa());

        Assert.Null(await servico.MigrarModeloReconhecimentoAsync(obra.Id, default));
        Assert.Equal("obra-antiga", (await db.Obras.FirstAsync(o => o.Id == obra.Id)).AzureFacePersonGroupId);
    }

    // O faceId da foto de assinatura precisa sair no mesmo modelo do grupo, senão o identify recusa.
    [Fact]
    public async Task IdentificarAsync_DetectaComOModeloDoGrupo()
    {
        var db = CriarDb(nameof(IdentificarAsync_DetectaComOModeloDoGrupo));
        var obra = new Obra
        {
            Codigo = "OB1", Nome = "Obra Teste", AzureFacePersonGroupId = "grupo-r4-teste",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
        };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        string? consultaDetect = null;
        var factory = new HttpClientFactoryFalso(req =>
        {
            if (req.Method == HttpMethod.Get)
                return Json(new { recognitionModel = "recognition_04" });
            if (req.RequestUri!.AbsolutePath.EndsWith("/detect"))
            {
                consultaDetect = req.RequestUri.Query;
                return Json(new List<object>());
            }
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });
        var servico = new AzureFaceAutenticacaoStrategy(db, factory, Opcoes());

        await servico.IdentificarAsync(obra.Id, new byte[] { 1 }, default);

        Assert.Contains("recognitionModel=recognition_04", consultaDetect);
        Assert.Contains("detectionModel=detection_03", consultaDetect);
    }

    // Consentimento revogado: o rosto até é reconhecido, mas a pessoa não pode mais assinar pela biometria.
    [Fact]
    public async Task IdentificarAsync_SemConsentimentoAtivo_Recusa()
    {
        var db = CriarDb(nameof(IdentificarAsync_SemConsentimentoAtivo_Recusa));
        var obra = new Obra
        {
            Codigo = "OB1", Nome = "Obra Teste", AzureFacePersonGroupId = "grupo-sem-consentimento",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
        };
        db.Obras.Add(obra);
        db.Trabalhadores.Add(new Trabalhador { ObraId = obra.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow, AzureFacePersonId = "person-1" });
        await db.SaveChangesAsync();
        var factory = new HttpClientFactoryFalso(req =>
        {
            var caminho = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Get) return Json(new { recognitionModel = "recognition_04" });
            if (caminho.EndsWith("/detect")) return Json(new[] { new { faceId = "face-1" } });
            if (caminho.EndsWith("/identify")) return Json(new[] { new { faceId = "face-1", candidates = new[] { new { personId = "person-1", confidence = 0.95 } } } });
            throw new InvalidOperationException("chamada inesperada: " + req.RequestUri);
        });

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AzureFaceAutenticacaoStrategy(db, factory, Opcoes()).IdentificarAsync(obra.Id, new byte[] { 1 }, default));
        Assert.Contains("consentimento", erro.Message);
    }
}
