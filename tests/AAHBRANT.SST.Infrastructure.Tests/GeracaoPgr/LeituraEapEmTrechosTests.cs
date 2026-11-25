using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

namespace AAHBRANT.SST.Infrastructure.Tests.GeracaoPgr;

public class LeituraEapEmTrechosTests
{
    [Fact]
    public void TextoPequeno_UmTrechoSo()
    {
        var trechos = ClaudeGeradorPgr.DividirEmTrechos("1 | Obra\n1.1 | Terraplenagem\n", 12_000);

        Assert.Single(trechos);
    }

    [Fact]
    public void TextoGrande_QuebraEmFimDeLinha_SemPerderNemRepetirLinha()
    {
        var linhas = Enumerable.Range(1, 2000).Select(i => $"1.{i} | Atividade número {i} da EAP").ToList();
        var texto = string.Join("\n", linhas);

        var trechos = ClaudeGeradorPgr.DividirEmTrechos(texto, 12_000);

        Assert.True(trechos.Count > 1);
        Assert.All(trechos, t => Assert.True(t.Length <= 12_000));
        var juntas = trechos.SelectMany(t => t.Split('\n', StringSplitOptions.RemoveEmptyEntries)).ToList();
        Assert.Equal(linhas, juntas);
    }

    [Fact]
    public void LinhaMaiorQueOLimite_ViraTrechoSozinha()
    {
        var longa = new string('x', 500);
        var trechos = ClaudeGeradorPgr.DividirEmTrechos($"a\n{longa}\nb", 100);

        Assert.Contains(trechos, t => t.Contains(longa));
    }
}
