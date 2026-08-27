namespace AAHBRANT.SST.AgenteBiometria.Leitores;

public interface IFingerprintMatcher
{
    // Retorna um score de 0 a 100 representando a similaridade entre a captura ao vivo e um
    // template cadastrado.
    double Comparar(byte[] capturaBruta, byte[] templateBruto);
}
