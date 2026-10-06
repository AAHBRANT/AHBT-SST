using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

public class ImagemBiometricaCriptografiaService : IImagemBiometricaCriptografia
{
    public string Criptografar(byte[] imagem) => TemplateBiometricoCriptografiaConversor.Criptografar(imagem);

    public byte[] Descriptografar(string cifradoBase64) => TemplateBiometricoCriptografiaConversor.Descriptografar(cifradoBase64);
}
