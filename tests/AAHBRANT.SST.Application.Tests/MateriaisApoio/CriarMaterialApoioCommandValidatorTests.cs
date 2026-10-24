using AAHBRANT.SST.Application.MateriaisApoio.Commands;

namespace AAHBRANT.SST.Application.Tests.MateriaisApoio;

public class CriarMaterialApoioCommandValidatorTests
{
    private static readonly byte[] ConteudoJpegValido = { 0xFF, 0xD8, 0xFF, 0x01, 0x02, 0x03 };
    private static readonly byte[] ConteudoDocxValido = { 0x50, 0x4B, 0x03, 0x04, 0x01, 0x02 };
    private static readonly byte[] ConteudoPdfValido = { 0x25, 0x50, 0x44, 0x46, 0x01, 0x02 };

    private static CriarMaterialApoioCommand ComandoValido() =>
        new("Regras da Casa", "Sinalização - Alojamento", "regras-da-casa.jpeg", "image/jpeg", ConteudoJpegValido);

    [Fact]
    public void Validate_ComandoValido_NaoRetornaErros()
    {
        var validator = new CriarMaterialApoioCommandValidator();

        var resultado = validator.Validate(ComandoValido());

        Assert.True(resultado.IsValid);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("image/png")]
    public void Validate_TiposSuportados_NaoRetornaErroDeContentType(string contentType)
    {
        var conteudo = contentType switch
        {
            "application/pdf" => ConteudoPdfValido,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ConteudoDocxValido,
            _ => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
        };
        var validator = new CriarMaterialApoioCommandValidator();
        var comando = ComandoValido() with { ContentType = contentType, Conteudo = conteudo };

        var resultado = validator.Validate(comando);

        Assert.True(resultado.IsValid);
    }

    [Fact]
    public void Validate_TipoNaoSuportado_RetornaErro()
    {
        var validator = new CriarMaterialApoioCommandValidator();
        var comando = ComandoValido() with { ContentType = "application/zip" };

        var resultado = validator.Validate(comando);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarMaterialApoioCommand.ContentType));
    }

    [Fact]
    public void Validate_ConteudoNaoBateComContentTypeDeclarado_RetornaErro()
    {
        var validator = new CriarMaterialApoioCommandValidator();
        // Declara PDF mas manda bytes de JPEG.
        var comando = ComandoValido() with { ContentType = "application/pdf", Conteudo = ConteudoJpegValido };

        var resultado = validator.Validate(comando);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarMaterialApoioCommand.Conteudo));
    }

    [Fact]
    public void Validate_NomeVazio_RetornaErro()
    {
        var validator = new CriarMaterialApoioCommandValidator();
        var comando = ComandoValido() with { Nome = "" };

        var resultado = validator.Validate(comando);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarMaterialApoioCommand.Nome));
    }
}
