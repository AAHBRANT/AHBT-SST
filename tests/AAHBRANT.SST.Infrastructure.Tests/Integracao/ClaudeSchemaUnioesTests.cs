using System.Net;
using System.Text;
using System.Text.Json;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;
using AAHBRANT.SST.Infrastructure.Tests.Assinatura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Tests.Integracao;

// 23/10/2026: o Claude recusa (400) schema com mais de 16 campos "tipo ou null"; a leitura do contrato
// de terceirizada tinha 17. Acima do limite, textos opcionais vão como string e "" volta a null.
public class ClaudeSchemaUnioesTests
{
    private record Pessoa(string Nome, string? Cpf);
    private record Resposta(string? A, List<Pessoa> Pessoas);

    private static object Texto(bool nulo = true) => new { type = nulo ? new[] { "string", "null" } : new[] { "string" } };

    private static object Objeto(Dictionary<string, object> campos) => new
    {
        type = "object", properties = campos, required = campos.Keys.ToArray(), additionalProperties = false,
    };

    private static (AzureOpenAiChatJsonCliente Chat, List<string> Corpos) Criar(string conteudoResposta)
    {
        var corpos = new List<string>();
        var fabrica = new HttpClientFactoryFalso(req =>
        {
            corpos.Add(req.Content!.ReadAsStringAsync().Result);
            var json = JsonSerializer.Serialize(new { content = new[] { new { type = "text", text = conteudoResposta } } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });
        var chat = new AzureOpenAiChatJsonCliente(fabrica, Options.Create(new AzureOpenAiOptions()),
            Options.Create(new ClaudeOptions { ApiKey = "chave-de-teste" }), NullLogger<AzureOpenAiChatJsonCliente>.Instance);
        return (chat, corpos);
    }

    private static int ContarListasDeTipo(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => e.EnumerateObject().Sum(p =>
            (p.Name == "type" && p.Value.ValueKind == JsonValueKind.Array ? 1 : 0) + ContarListasDeTipo(p.Value)),
        JsonValueKind.Array => e.EnumerateArray().Sum(ContarListasDeTipo),
        _ => 0,
    };

    [Fact]
    public async Task SchemaComMaisDe16Unioes_VaiSemUniaoEVazioVoltaNulo()
    {
        var campos = new Dictionary<string, object> { ["a"] = Texto() };
        for (var i = 0; i < 16; i++) campos[$"extra{i}"] = Texto();
        campos["pessoas"] = new { type = "array", items = Objeto(new() { ["nome"] = Texto(nulo: false), ["cpf"] = Texto() }) };
        var (chat, corpos) = Criar("""{"a":"","pessoas":[{"nome":"João","cpf":""},{"nome":"Ana","cpf":"123"}]}""");

        var r = await chat.ObterAsync<Resposta>("sistema", "usuario", "teste", Objeto(campos), CancellationToken.None);

        using var corpo = JsonDocument.Parse(Assert.Single(corpos));
        Assert.Equal(0, ContarListasDeTipo(corpo.RootElement.GetProperty("output_config")));
        Assert.Null(r.A);
        Assert.Null(r.Pessoas[0].Cpf);
        Assert.Equal("123", r.Pessoas[1].Cpf);
        Assert.Equal("João", r.Pessoas[0].Nome);
    }

    [Fact]
    public async Task SchemaDentroDoLimite_MantemNuloNoSchema()
    {
        var (chat, corpos) = Criar("""{"a":null,"pessoas":[]}""");
        var schema = Objeto(new() { ["a"] = Texto(), ["pessoas"] = new { type = "array", items = Objeto(new() { ["nome"] = Texto(nulo: false), ["cpf"] = Texto() }) } });

        var r = await chat.ObterAsync<Resposta>("sistema", "usuario", "teste", schema, CancellationToken.None);

        Assert.Contains("\"null\"", Assert.Single(corpos));
        Assert.Null(r.A);
    }
}
