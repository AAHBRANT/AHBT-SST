namespace AAHBRANT.SST.AgenteBiometria.Leitores;

// Implementação simulada — comparação byte-a-byte, não é um algoritmo biométrico real. Serve para
// desenvolvimento/testes sem hardware; troca por um matcher real do SDK Futronic quando o hardware
// chegar (fora do escopo deste plano).
public class SimuladoFingerprintMatcher : IFingerprintMatcher
{
    public double Comparar(byte[] capturaBruta, byte[] templateBruto)
    {
        if (capturaBruta.Length == 0 || templateBruto.Length == 0)
        {
            return 0;
        }

        var tamanhoComum = Math.Min(capturaBruta.Length, templateBruto.Length);
        var iguais = 0;
        for (var i = 0; i < tamanhoComum; i++)
        {
            if (capturaBruta[i] == templateBruto[i])
            {
                iguais++;
            }
        }

        return 100.0 * iguais / Math.Max(capturaBruta.Length, templateBruto.Length);
    }
}
