using System.Text.Json;
using AAHBRANT.SST.Infrastructure.Integracao.Grh;

namespace AAHBRANT.SST.Infrastructure.Tests.Integracao;

public class ColaboradorGrhPayloadTests
{
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Deserializar_DataExperiencia2FimEmFormatoInvalido_NaoLancaEViraNull()
    {
        // Incidente real (22/09): um colaborador do G-RH trouxe "experiencia2Fim": "" (ou outro
        // valor fora do ISO 8601), o que derrubava a desserialização do LOTE INTEIRO — ninguém
        // era importado, não só esse colaborador.
        var json = """
        {
            "cpf": "12345678900",
            "nome": "Fulano de Tal",
            "admissao": "2026-01-10T00:00:00",
            "experiencia2Fim": "data-invalida"
        }
        """;

        var payload = JsonSerializer.Deserialize<ColaboradorGrhPayload>(json, Opcoes);

        Assert.NotNull(payload);
        Assert.Null(payload!.Experiencia2Fim);
        Assert.Equal("Fulano de Tal", payload.Nome);
    }

    [Fact]
    public void Deserializar_DataExperiencia2FimVazia_ViraNull()
    {
        var json = """
        {
            "cpf": "12345678900",
            "nome": "Fulano de Tal",
            "admissao": "2026-01-10T00:00:00",
            "experiencia2Fim": ""
        }
        """;

        var payload = JsonSerializer.Deserialize<ColaboradorGrhPayload>(json, Opcoes);

        Assert.NotNull(payload);
        Assert.Null(payload!.Experiencia2Fim);
    }

    [Fact]
    public void Deserializar_DataExperiencia2FimNula_PermaneceNull()
    {
        var json = """
        {
            "cpf": "12345678900",
            "nome": "Fulano de Tal",
            "admissao": "2026-01-10T00:00:00",
            "experiencia2Fim": null
        }
        """;

        var payload = JsonSerializer.Deserialize<ColaboradorGrhPayload>(json, Opcoes);

        Assert.NotNull(payload);
        Assert.Null(payload!.Experiencia2Fim);
    }

    [Fact]
    public void Deserializar_DataExperiencia2FimValida_ContinuaDesserializandoNormalmente()
    {
        var json = """
        {
            "cpf": "12345678900",
            "nome": "Fulano de Tal",
            "admissao": "2026-01-10T00:00:00",
            "experiencia2Fim": "2026-07-10T00:00:00"
        }
        """;

        var payload = JsonSerializer.Deserialize<ColaboradorGrhPayload>(json, Opcoes);

        Assert.NotNull(payload);
        Assert.Equal(new DateTime(2026, 7, 10), payload!.Experiencia2Fim);
    }

    [Fact]
    public void Deserializar_ListaComUmColaboradorComDataInvalida_NaoDerrubaOsOutros()
    {
        // Prova direta do bug original: um item com data ruim, no meio da lista, não pode
        // impedir os outros de serem desserializados.
        var json = """
        [
            { "cpf": "11111111111", "nome": "Antes", "admissao": "2025-01-01T00:00:00" },
            { "cpf": "22222222222", "nome": "Com Data Ruim", "admissao": "2025-01-01T00:00:00", "experiencia2Fim": "31-13-2026" },
            { "cpf": "33333333333", "nome": "Depois", "admissao": "2025-01-01T00:00:00" }
        ]
        """;

        var lista = JsonSerializer.Deserialize<List<ColaboradorGrhPayload>>(json, Opcoes);

        Assert.NotNull(lista);
        Assert.Equal(3, lista!.Count);
        Assert.Equal("Antes", lista[0].Nome);
        Assert.Equal("Com Data Ruim", lista[1].Nome);
        Assert.Null(lista[1].Experiencia2Fim);
        Assert.Equal("Depois", lista[2].Nome);
    }
}
