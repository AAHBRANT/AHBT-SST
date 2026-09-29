using SourceAFIS;

namespace AAHBRANT.SST.AgenteBiometria.Leitores;

// Match real com SourceAFIS (código aberto), sobre a imagem 320x480 a 500 dpi do FS80H.
// Escala: o score do SourceAFIS (~40 = 1 falso positivo em 10.000) é normalizado para 0-100 de modo
// que 80 do SourceAFIS = 100; assim o limiar padrão do backend (50) equivale ao 40 do SourceAFIS.
public class SourceAfisFingerprintMatcher : IFingerprintMatcher
{
    public const int Largura = 320;
    public const int Altura = 480;
    private const double ScoreQueVale100 = 80.0;

    private readonly bool _inverterImagem;

    public SourceAfisFingerprintMatcher(bool inverterImagem = false)
    {
        _inverterImagem = inverterImagem;
    }

    public byte[] ExtrairTemplate(byte[] capturaBruta)
    {
        if (capturaBruta.Length != Largura * Altura)
        {
            throw new ArgumentException(
                $"Imagem com {capturaBruta.Length} bytes; esperado {Largura * Altura} ({Largura}x{Altura}, 8 bits).", nameof(capturaBruta));
        }

        var pixels = _inverterImagem ? capturaBruta.Select(b => (byte)(255 - b)).ToArray() : capturaBruta;
        var imagem = new FingerprintImage(Largura, Altura, pixels, new FingerprintImageOptions { Dpi = 500 });
        return new FingerprintTemplate(imagem).ToByteArray();
    }

    public double Comparar(byte[] capturaBruta, byte[] templateBruto)
    {
        if (capturaBruta.Length == 0 || templateBruto.Length == 0)
        {
            return 0;
        }

        var sonda = new FingerprintTemplate(capturaBruta);
        var candidato = new FingerprintTemplate(templateBruto);
        var score = new FingerprintMatcher(sonda).Match(candidato);

        return Math.Min(100.0, 100.0 * score / ScoreQueVale100);
    }
}
