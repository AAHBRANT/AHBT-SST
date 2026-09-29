namespace AAHBRANT.SST.AgenteBiometria.Leitores;

public interface IFingerprintMatcher
{
    // Converte a captura bruta do leitor no template usado por Comparar (e guardado no cadastro).
    byte[] ExtrairTemplate(byte[] capturaBruta);

    // Retorna um score de 0 a 100 representando a similaridade entre um template extraído da
    // captura ao vivo e um template cadastrado (ambos no formato de ExtrairTemplate).
    double Comparar(byte[] capturaBruta, byte[] templateBruto);
}
