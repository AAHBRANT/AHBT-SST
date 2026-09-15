using AAHBRANT.SST.Application.Pgrs.Commands;

namespace AAHBRANT.SST.Application.Tests.Pgrs;

public class AnexarDocumentoPgrCommandValidatorTests
{
    private static readonly byte[] PdfValido = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

    private static AnexarDocumentoPgrCommand ComandoValido() =>
        new(Guid.NewGuid(), PdfValido, "application/pdf");

    [Fact]
    public void Validate_ComandoValido_NaoRetornaErros()
    {
        var validator = new AnexarDocumentoPgrCommandValidator();

        var resultado = validator.Validate(ComandoValido());

        Assert.True(resultado.IsValid);
    }

    [Fact]
    public void Validate_ConteudoMaiorQue20Mb_RetornaErro()
    {
        var validator = new AnexarDocumentoPgrCommandValidator();
        var conteudoGrande = new byte[20 * 1024 * 1024 + 1];
        Array.Copy(PdfValido, conteudoGrande, PdfValido.Length);
        var comando = ComandoValido() with { Conteudo = conteudoGrande };

        var resultado = validator.Validate(comando);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(AnexarDocumentoPgrCommand.Conteudo));
    }

    [Fact]
    public void Validate_ContentTypeDiferenteDePdf_RetornaErro()
    {
        var validator = new AnexarDocumentoPgrCommandValidator();
        var comando = ComandoValido() with { ContentType = "image/png" };

        var resultado = validator.Validate(comando);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(AnexarDocumentoPgrCommand.ContentType));
    }

    [Fact]
    public void Validate_ConteudoNaoBateComAssinaturaPdf_RetornaErro()
    {
        var validator = new AnexarDocumentoPgrCommandValidator();
        var comando = ComandoValido() with { Conteudo = new byte[] { 0x00, 0x01, 0x02, 0x03 } };

        var resultado = validator.Validate(comando);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(AnexarDocumentoPgrCommand.Conteudo));
    }
}
