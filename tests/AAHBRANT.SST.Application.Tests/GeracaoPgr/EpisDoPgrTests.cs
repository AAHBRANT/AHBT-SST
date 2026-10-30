using AAHBRANT.SST.Application.GeracaoPgr;

namespace AAHBRANT.SST.Application.Tests.GeracaoPgr;

// Tipo de EPI reconhecido no texto das medidas do PGR (30/10/2026).
public class EpisDoPgrTests
{
    [Theory]
    [InlineData("Use capacete ao circular e trabalhar em áreas com risco de queda de materiais.", "Capacete de segurança")]
    [InlineData("Use calçado fechado com solado antiderrapante.", "Calçado de segurança")]
    [InlineData("Use botina de segurança.", "Calçado de segurança")]
    [InlineData("Use bota de PVC na concretagem.", "Bota impermeável")]
    [InlineData("Use colete de alta visibilidade ao circular em áreas com veículos.", "Colete refletivo")]
    [InlineData("Use luvas para segurar os materiais.", "Luva de segurança")]
    [InlineData("Use luva isolante na instalação elétrica.", "Luva isolante")]
    [InlineData("Use óculos de segurança.", "Óculos de segurança")]
    [InlineData("Use protetor auricular tipo plug.", "Protetor auricular")]
    [InlineData("Use máscara PFF2 no corte de concreto.", "Respirador PFF2")]
    [InlineData("Use máscara de solda.", "Máscara de solda")]
    [InlineData("Use cinto de segurança tipo paraquedista com talabarte duplo.", "Cinturão paraquedista")]
    public void Reconhecer_TipoUnico(string texto, string esperado) =>
        Assert.Equal(new[] { esperado }, EpisDoPgr.Reconhecer(texto).Select(t => t.Nome).ToArray());

    [Fact]
    public void Reconhecer_VariosTiposNaMesmaFrase_NaOrdemDaTabela() =>
        Assert.Equal(new[] { "Capacete de segurança", "Calçado de segurança", "Luva de segurança" },
            EpisDoPgr.Reconhecer("Use luvas, capacete e calçado de segurança durante o recebimento.").Select(t => t.Nome).ToArray());

    [Theory]
    [InlineData("Use EPI adequado.")]
    [InlineData("")]
    [InlineData(null)]
    public void Reconhecer_SemTipoConhecido_Vazio(string? texto) => Assert.Empty(EpisDoPgr.Reconhecer(texto));
}
