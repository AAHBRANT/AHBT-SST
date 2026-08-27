using AAHBRANT.SST.Infrastructure.Seguranca;

namespace AAHBRANT.SST.Infrastructure.Tests.Seguranca;

public class TemplateBiometricoCriptografiaConversorTests
{
    public TemplateBiometricoCriptografiaConversorTests()
    {
        var chave = new byte[32];
        Array.Fill(chave, (byte)7);
        TemplateBiometricoCriptografiaContexto.Configurar(chave);
    }

    [Fact]
    public void Criptografar_DeveGerarStringDiferenteDoOriginal()
    {
        var templateBruto = new byte[] { 1, 2, 3, 4, 5 };

        var cifrado = TemplateBiometricoCriptografiaConversor.Criptografar(templateBruto);

        Assert.False(string.IsNullOrWhiteSpace(cifrado));
    }

    [Fact]
    public void Criptografar_ChamadoDuasVezesComMesmoInput_DeveGerarCifradosDiferentes()
    {
        var templateBruto = new byte[] { 1, 2, 3, 4, 5 };

        var cifrado1 = TemplateBiometricoCriptografiaConversor.Criptografar(templateBruto);
        var cifrado2 = TemplateBiometricoCriptografiaConversor.Criptografar(templateBruto);

        Assert.NotEqual(cifrado1, cifrado2);
    }

    [Fact]
    public void Descriptografar_DeveRecuperarOTemplateOriginal()
    {
        var templateBruto = new byte[] { 10, 20, 30, 40, 50, 60 };

        var cifrado = TemplateBiometricoCriptografiaConversor.Criptografar(templateBruto);
        var recuperado = TemplateBiometricoCriptografiaConversor.Descriptografar(cifrado);

        Assert.Equal(templateBruto, recuperado);
    }
}
