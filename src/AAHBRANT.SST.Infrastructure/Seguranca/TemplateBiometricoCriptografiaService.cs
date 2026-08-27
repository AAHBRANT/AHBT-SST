using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

public class TemplateBiometricoCriptografiaService : ITemplateBiometricoCriptografia
{
    public string Criptografar(byte[] templateBruto) =>
        TemplateBiometricoCriptografiaConversor.Criptografar(templateBruto);
}
