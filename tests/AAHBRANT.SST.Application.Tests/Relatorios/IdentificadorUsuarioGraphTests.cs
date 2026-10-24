using AAHBRANT.SST.Infrastructure.Integracao.Teams;

namespace AAHBRANT.SST.Application.Tests.Relatorios;

// O sininho do Teams usa o id do Azure AD quando existe e, para quem ainda não entrou no app, o e-mail (UPN).
public class IdentificadorUsuarioGraphTests
{
    [Fact]
    public void ComAzureAdObjectId_UsaOId()
    {
        var id = Guid.NewGuid().ToString();

        Assert.Equal(id, IdentificadorUsuarioGraph.Obter(id, "junior.peixoto@aahbrant.com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SemAzureAdObjectId_UsaOEmail(string? idVazio)
    {
        Assert.Equal("junior.peixoto@aahbrant.com", IdentificadorUsuarioGraph.Obter(idVazio, "  junior.peixoto@aahbrant.com "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem-arroba")]
    [InlineData("a@b.com/../admin")]
    [InlineData("a@b.com?x=1")]
    [InlineData("a b@c.com")]
    [InlineData("a%40b.com")]
    public void EmailInvalidoOuPerigoso_NaoEUsado(string? email)
    {
        Assert.Null(IdentificadorUsuarioGraph.Obter(null, email));
    }
}
