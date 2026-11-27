using System.Net;
using System.Text;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Assinatura;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Exclusão aprovada das pessoas soltas (etapa 2 do agente engenheiro): confere de novo na hora, nunca apaga
// quem tem funcionário ligado e não treina grupo que ficou vazio (o Azure recusa: "no persisted face" —
// achado no teste real de 27/11).
public class ExcluirPessoasFaciaisSoltasTests
{
    private sealed class AzureFalso : HttpMessageHandler
    {
        private readonly string _pessoasJson;
        public List<string> Chamadas { get; } = new();

        public AzureFalso(string pessoasJson) => _pessoasJson = pessoasJson;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.PathAndQuery;
            Chamadas.Add($"{request.Method} {url}");
            var corpo = url.Contains("/persons?") ? _pessoasJson
                : url.EndsWith("/training") ? "{\"status\":\"succeeded\"}"
                : "{}";
            var status = request.Method == HttpMethod.Post && url.EndsWith("/train") ? HttpStatusCode.Accepted : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class FabricaFalsa(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static async Task<(AzureFaceAutenticacaoStrategy Estrategia, AzureFalso Azure, Guid ObraId)> CriarAsync(string pessoasJson, bool comFuncionarioLigado)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra", Codigo = "O", AzureFacePersonGroupId = "grupo-obra" };
        db.Add(obra);
        if (comFuncionarioLigado)
            db.Add(new Trabalhador { Nome = "Ana", Obra = obra, ObraId = obra.Id, AzureFacePersonId = "ligada" });
        await db.SaveChangesAsync();
        var azure = new AzureFalso(pessoasJson);
        var opcoes = Options.Create(new AssinaturaOptions { AzureFaceApiEndpoint = "https://azure.invalid", AzureFaceApiKey = "chave-de-teste" });
        return (new AzureFaceAutenticacaoStrategy(db, new FabricaFalsa(azure), opcoes), azure, obra.Id);
    }

    private const string TresPessoas =
        "[{\"personId\":\"ligada\",\"name\":\"Ana\",\"persistedFaceIds\":[\"f1\"]}," +
        "{\"personId\":\"solta-1\",\"name\":\"Ana\",\"persistedFaceIds\":[\"f2\"]}," +
        "{\"personId\":\"solta-2\",\"name\":\"X\",\"persistedFaceIds\":[\"f3\"]}]";

    [Fact]
    public async Task ExcluiSoAsAindaSoltas_NuncaQuemTemFuncionario_ERetreina()
    {
        var (estrategia, azure, obraId) = await CriarAsync(TresPessoas, comFuncionarioLigado: true);

        // Mesmo pedindo a "ligada" (proposta antiga ou adulterada), ela não sai.
        var excluidas = await estrategia.ExcluirPessoasSemVinculoAsync(obraId, ["solta-1", "solta-2", "ligada"], CancellationToken.None);

        Assert.Equal(2, excluidas);
        var exclusoes = azure.Chamadas.Where(c => c.StartsWith("DELETE")).ToList();
        Assert.Equal(2, exclusoes.Count);
        Assert.DoesNotContain(exclusoes, c => c.Contains("/ligada"));
        Assert.Contains(azure.Chamadas, c => c == "POST /face/v1.0/persongroups/grupo-obra/train");
    }

    [Fact]
    public async Task GrupoQueFicouVazio_NaoTreina()
    {
        var (estrategia, azure, obraId) = await CriarAsync(
            "[{\"personId\":\"solta-1\",\"name\":\"A\",\"persistedFaceIds\":[\"f1\"]},{\"personId\":\"solta-2\",\"name\":\"B\",\"persistedFaceIds\":[\"f2\"]}]",
            comFuncionarioLigado: false);

        var excluidas = await estrategia.ExcluirPessoasSemVinculoAsync(obraId, ["solta-1", "solta-2"], CancellationToken.None);

        Assert.Equal(2, excluidas);
        Assert.DoesNotContain(azure.Chamadas, c => c.EndsWith("/train"));
    }

    [Fact]
    public async Task NadaMaisSolto_NaoChamaOAzureParaExcluir()
    {
        var (estrategia, azure, obraId) = await CriarAsync("[{\"personId\":\"ligada\",\"name\":\"Ana\",\"persistedFaceIds\":[\"f1\"]}]", comFuncionarioLigado: true);

        Assert.Equal(0, await estrategia.ExcluirPessoasSemVinculoAsync(obraId, ["solta-1"], CancellationToken.None));
        Assert.DoesNotContain(azure.Chamadas, c => c.StartsWith("DELETE") || c.EndsWith("/train"));
    }
}
