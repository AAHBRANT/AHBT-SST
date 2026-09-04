using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Application.Tests.Common;

public class ValidadorAssinaturaArquivoTests
{
    private static readonly byte[] AssinaturaJpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
    private static readonly byte[] AssinaturaPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
    private static readonly byte[] AssinaturaPdf = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
    private static readonly byte[] ConteudoFalsificado = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("application/pdf")]
    public void AssinaturaConfere_DeveAceitarArquivoComAssinaturaCorreta(string contentType)
    {
        var conteudo = contentType switch
        {
            "image/jpeg" => AssinaturaJpeg,
            "image/png" => AssinaturaPng,
            _ => AssinaturaPdf,
        };

        Assert.True(ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, contentType));
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("application/pdf")]
    public void AssinaturaConfere_DeveRejeitarConteudoQueNaoBateComOTipoDeclarado(string contentType)
    {
        Assert.False(ValidadorAssinaturaArquivo.AssinaturaConfere(ConteudoFalsificado, contentType));
    }

    [Fact]
    public void AssinaturaConfere_DeveRejeitarArquivoTrocadoDeTipo()
    {
        // Um PNG renomeado/reenviado com Content-Type "image/jpeg" — exatamente o cenário que a
        // checagem de Content-Type sozinha (sem olhar os bytes) deixava passar.
        Assert.False(ValidadorAssinaturaArquivo.AssinaturaConfere(AssinaturaPng, "image/jpeg"));
    }

    [Fact]
    public void AssinaturaConfere_DeveRejeitarContentTypeNaoSuportado()
    {
        Assert.False(ValidadorAssinaturaArquivo.AssinaturaConfere(AssinaturaJpeg, "application/octet-stream"));
    }

    [Fact]
    public void AssinaturaConfere_DeveRejeitarArquivoVazio()
    {
        Assert.False(ValidadorAssinaturaArquivo.AssinaturaConfere(Array.Empty<byte>(), "image/jpeg"));
    }
}
