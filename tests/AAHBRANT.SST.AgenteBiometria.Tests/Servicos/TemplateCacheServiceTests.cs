using System.Security.Cryptography;
using AAHBRANT.SST.AgenteBiometria.Servicos;

namespace AAHBRANT.SST.AgenteBiometria.Tests.Servicos;

public class TemplateCacheServiceTests
{
    private static string CriptografarComoOBackendFaria(byte[] templateBruto, byte[] chave)
    {
        const int tamanhoNonce = 12;
        const int tamanhoTag = 16;
        var nonce = RandomNumberGenerator.GetBytes(tamanhoNonce);
        var cifrado = new byte[templateBruto.Length];
        var tag = new byte[tamanhoTag];

        using var aesGcm = new AesGcm(chave, tamanhoTag);
        aesGcm.Encrypt(nonce, templateBruto, cifrado, tag);

        var resultado = new byte[tamanhoNonce + cifrado.Length + tamanhoTag];
        Buffer.BlockCopy(nonce, 0, resultado, 0, tamanhoNonce);
        Buffer.BlockCopy(cifrado, 0, resultado, tamanhoNonce, cifrado.Length);
        Buffer.BlockCopy(tag, 0, resultado, tamanhoNonce + cifrado.Length, tamanhoTag);
        return Convert.ToBase64String(resultado);
    }

    [Fact]
    public void DescriptografarTemplate_DeveRecuperarOTemplateOriginal()
    {
        var chave = new byte[32];
        Array.Fill(chave, (byte)9);
        var templateOriginal = new byte[] { 11, 22, 33, 44 };
        var cifrado = CriptografarComoOBackendFaria(templateOriginal, chave);

        var recuperado = TemplateCacheService.DescriptografarTemplate(cifrado, chave);

        Assert.Equal(templateOriginal, recuperado);
    }
}
