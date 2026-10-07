using System.Net;
using System.Text;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Assinatura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Fila facial 1:N (ninguém escolhe a pessoa): o primeiro colocado precisa estar bem à frente do segundo,
// senão a leitura é recusada (colega parecido). E toda falha é registrada para a lista de cadastros fracos.
public class IdentificacaoFacialMargemTests
{
    private sealed class AzureFalso : HttpMessageHandler
    {
        private readonly string _candidatosJson;
        public AzureFalso(string candidatosJson) => _candidatosJson = candidatosJson;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.PathAndQuery;
            var corpo = url.Contains("/detect") ? "[{\"faceId\":\"face-1\"}]"
                : url.Contains("/identify") ? $"[{{\"faceId\":\"face-1\",\"candidates\":{_candidatosJson}}}]"
                : "{\"recognitionModel\":\"recognition_04\"}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FabricaFalsa : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FabricaFalsa(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static async Task<(AzureFaceAutenticacaoStrategy Estrategia, Guid ObraId, Guid TrabalhadorId, Infrastructure.Persistencia.SstDbContext Db)> CriarAsync(string candidatosJson)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra
        {
            Nome = "Obra",
            Codigo = "O",
            AzureFacePersonGroupId = "grupo-obra",
            MetodosAutenticacaoHabilitados = MetodoAutenticacaoObra.ReconhecimentoFacial,
        };
        var ana = new Trabalhador { Nome = "Ana", Obra = obra, ObraId = obra.Id, AzureFacePersonId = "p1" };
        var bruno = new Trabalhador { Nome = "Bruno", Obra = obra, ObraId = obra.Id, AzureFacePersonId = "p2" };
        db.AddRange(obra, ana, bruno);
        await db.SaveChangesAsync();

        var opcoes = Options.Create(new AssinaturaOptions
        {
            AzureFaceApiEndpoint = "https://azure.invalid",
            AzureFaceApiKey = "chave-de-teste",
            LimiarConfiancaFacial = 0.85,
            LimiarConfiancaFacialMinimo = 0.60,
            MargemMinimaIdentificacaoFacial = 0.10,
        });
        return (new AzureFaceAutenticacaoStrategy(db, new FabricaFalsa(new AzureFalso(candidatosJson)), opcoes), obra.Id, ana.Id, db);
    }

    private static readonly byte[] Foto = { 1, 2, 3 };

    [Fact]
    public async Task Fila_SegundoColocadoPerto_EhRecusadoComoAmbiguoESemAtribuirAlguem()
    {
        var (estrategia, obraId, _, db) = await CriarAsync("[{\"personId\":\"p1\",\"confidence\":0.92},{\"personId\":\"p2\",\"confidence\":0.88}]");

        var r = await estrategia.IdentificarAsync(obraId, Foto, CancellationToken.None, exigirMargemSobreSegundoColocado: true);

        Assert.False(r.Aceito);
        Assert.Equal(MotivoRejeicaoFacial.RostoAmbiguo, r.Motivo);
        var falha = await db.FalhasReconhecimentoFacial.SingleAsync();
        Assert.Equal(MotivoFalhaFacial.RostoAmbiguo, falha.Motivo);
        Assert.Null(falha.TrabalhadorId); // pode ser qualquer um dos dois: não atribui
    }

    [Fact]
    public async Task SemFila_MesmaSituacao_EhAceita()
    {
        var (estrategia, obraId, anaId, _) = await CriarAsync("[{\"personId\":\"p1\",\"confidence\":0.92},{\"personId\":\"p2\",\"confidence\":0.88}]");

        var r = await estrategia.IdentificarAsync(obraId, Foto, CancellationToken.None);

        Assert.True(r.Aceito);
        Assert.Equal(anaId, r.Resultado!.TrabalhadorId);
    }

    [Fact]
    public async Task Fila_MargemSuficiente_EhAceita()
    {
        var (estrategia, obraId, anaId, db) = await CriarAsync("[{\"personId\":\"p1\",\"confidence\":0.95},{\"personId\":\"p2\",\"confidence\":0.60}]");

        var r = await estrategia.IdentificarAsync(obraId, Foto, CancellationToken.None, exigirMargemSobreSegundoColocado: true);

        Assert.True(r.Aceito);
        Assert.Equal(anaId, r.Resultado!.TrabalhadorId);
        Assert.Empty(db.FalhasReconhecimentoFacial);
    }

    [Fact]
    public async Task Fila_UmSoCandidato_EhAceito()
    {
        var (estrategia, obraId, anaId, _) = await CriarAsync("[{\"personId\":\"p1\",\"confidence\":0.93}]");

        var r = await estrategia.IdentificarAsync(obraId, Foto, CancellationToken.None, exigirMargemSobreSegundoColocado: true);

        Assert.True(r.Aceito);
        Assert.Equal(anaId, r.Resultado!.TrabalhadorId);
    }

    [Fact]
    public async Task ConfiancaBaixa_RegistraFalhaAtribuidaAoTrabalhador()
    {
        var (estrategia, obraId, anaId, db) = await CriarAsync("[{\"personId\":\"p1\",\"confidence\":0.75}]");

        var r = await estrategia.IdentificarAsync(obraId, Foto, CancellationToken.None);

        Assert.False(r.Aceito);
        Assert.Equal(MotivoRejeicaoFacial.ConfiancaBaixa, r.Motivo);
        Assert.Equal(anaId, r.TrabalhadorIdProvavel);
        var falha = await db.FalhasReconhecimentoFacial.SingleAsync();
        Assert.Equal(MotivoFalhaFacial.ConfiancaBaixa, falha.Motivo);
        Assert.Equal(anaId, falha.TrabalhadorId);
        Assert.Equal(0.75, falha.Confianca);
    }

    [Fact]
    public async Task ConfiancaMuitoBaixa_RegistraFalhaSemAtribuir()
    {
        var (estrategia, obraId, _, db) = await CriarAsync("[{\"personId\":\"p1\",\"confidence\":0.55}]");

        var r = await estrategia.IdentificarAsync(obraId, Foto, CancellationToken.None);

        Assert.Equal(MotivoRejeicaoFacial.RostoNaoReconhecido, r.Motivo);
        var falha = await db.FalhasReconhecimentoFacial.SingleAsync();
        Assert.Null(falha.TrabalhadorId); // abaixo do mínimo, o rosto pode ser de outra pessoa
    }

    [Fact]
    public async Task SemCandidato_RegistraFalhaNaoReconhecido()
    {
        var (estrategia, obraId, _, db) = await CriarAsync("[]");

        var r = await estrategia.IdentificarAsync(obraId, Foto, CancellationToken.None);

        Assert.Equal(MotivoRejeicaoFacial.RostoNaoReconhecido, r.Motivo);
        Assert.Equal(MotivoFalhaFacial.RostoNaoReconhecido, (await db.FalhasReconhecimentoFacial.SingleAsync()).Motivo);
    }
}
