using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Application.Tests.Common;

public class FuncaoSstClassifierTests
{
    [Theory]
    [InlineData("Técnico de Segurança do Trabalho")]
    [InlineData("Tec. Segurança Do Trabalho")]
    [InlineData("TÉC. SEGURANÇA DO TRABALHO")]
    [InlineData("Tec Seguranca do Trabalho")]
    [InlineData("Técnico de Segurança")]
    public void EhTecnicoSeguranca_AceitaFormaAbreviada(string funcao) =>
        Assert.True(FuncaoSstClassifier.EhTecnicoSeguranca(funcao));

    [Theory]
    [InlineData("Engenheiro de Segurança do Trabalho")]
    [InlineData("Eng. Segurança do Trabalho")]
    public void EhEngenheiroSeguranca_AceitaFormaAbreviada(string funcao) =>
        Assert.True(FuncaoSstClassifier.EhEngenheiroSeguranca(funcao));

    [Theory]
    [InlineData("Auxiliar de Segurança do Trabalho")]
    [InlineData("Vigia - Segurança")]
    [InlineData("Técnico de Edificações")]
    [InlineData(null)]
    public void OutrasFuncoes_NaoSaoTecnicoNemEngenheiro(string? funcao)
    {
        Assert.False(FuncaoSstClassifier.EhTecnicoSeguranca(funcao));
        Assert.False(FuncaoSstClassifier.EhEngenheiroSeguranca(funcao));
    }
}
