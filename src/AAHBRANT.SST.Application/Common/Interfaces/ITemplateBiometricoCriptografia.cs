namespace AAHBRANT.SST.Application.Common.Interfaces;

// Só expõe criptografar — o backend nunca precisa (nem deve) descriptografar um template fora do
// fluxo de cadastro. Garantia estrutural: quem depende só desta interface não consegue ler biometria.
public interface ITemplateBiometricoCriptografia
{
    string Criptografar(byte[] templateBruto);
}
