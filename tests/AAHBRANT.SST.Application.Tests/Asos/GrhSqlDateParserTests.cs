using AAHBRANT.SST.Infrastructure.Integracao.Grh;

namespace AAHBRANT.SST.Application.Tests.Asos;

// O banco do G-RH guarda data como texto livre, em mais de um formato dentro do mesmo par de
// tabelas (achado na inspeção de schema de 2026-09-16) — este parser nunca deve lançar exceção,
// só retornar nulo pra formato desconhecido.
public class GrhSqlDateParserTests
{
    [Theory]
    [InlineData("2026-09-15", 2026, 9, 15)]
    [InlineData("2026-09-15T17:15:36", 2026, 9, 15)]
    [InlineData("2026-09-15T17:15:36.123", 2026, 9, 15)]
    [InlineData("15/09/2026", 2026, 9, 15)]
    public void Analisar_FormatosConhecidos_RetornaDataCorreta(string valor, int ano, int mes, int dia)
    {
        var resultado = GrhSqlDateParser.Analisar(valor);

        Assert.NotNull(resultado);
        Assert.Equal(ano, resultado!.Value.Year);
        Assert.Equal(mes, resultado.Value.Month);
        Assert.Equal(dia, resultado.Value.Day);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("não é uma data")]
    [InlineData("32/13/2026")]
    public void Analisar_ValorInvalido_RetornaNuloSemLancar(string? valor)
    {
        var resultado = GrhSqlDateParser.Analisar(valor);

        Assert.Null(resultado);
    }
}
