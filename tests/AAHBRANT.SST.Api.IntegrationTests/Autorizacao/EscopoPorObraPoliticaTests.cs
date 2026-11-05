using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Api.Middlewares;

namespace AAHBRANT.SST.Api.IntegrationTests.Autorizacao;

public class EscopoPorObraPoliticaTests
{
    // Caso real (05/11/2026): engenheiro aprovando o PGR da Beira Rio ficava sem obra nenhuma e a assinatura falhava.
    [Theory]
    [InlineData(PoliticasAutorizacao.SomenteEngenheiroSeguranca)]
    [InlineData(PoliticasAutorizacao.SomenteAdministrador)]
    [InlineData(PoliticasAutorizacao.QualquerUsuarioAutenticado)]
    public void PoliticaDePerfilNaoRestringeAsObras(string politica)
        => Assert.False(EscopoPorObraMiddleware.RestringeEscopo(politica));

    [Fact]
    public void PermissaoComumRestringeAsObras()
        => Assert.True(EscopoPorObraMiddleware.RestringeEscopo("pgr:ver"));
}
