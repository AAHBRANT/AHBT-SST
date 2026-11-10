using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Cameras;

// Isola a IA do resto do módulo: cadastro, registro e alerta não sabem qual motor analisa a foto.
// Lança InvalidOperationException quando a análise não pôde ser feita ou veio incoerente.
public interface IAnalisadorEpiImagem
{
    Task<ResultadoAnaliseEpi> AnalisarAsync(byte[] imagemJpeg, CancellationToken ct);
}

// Uma pessoa vista na foto: posição aproximada do centro da cabeça em fração da imagem (0 a 1, a partir
// do canto superior esquerdo) e o que falta. Serve para ligar cada rosto reconhecido à pessoa certa
// quando há mais de uma na cena.
public record PessoaNaCena(double CabecaX, double CabecaY, bool SemCapacete, bool SemColete)
{
    public bool Irregular => SemCapacete || SemColete;
}

// Detalhe: uma entrada por pessoa visível. Nulo quando a IA não informou posições coerentes com a contagem;
// nesse caso a cena com várias pessoas fica sem identificação por rosto.
public record ResultadoAnaliseEpi(int Pessoas, int SemCapacete, int SemColete, ConfiancaAnaliseEpi Confianca,
    IReadOnlyList<PessoaNaCena>? Detalhe = null);
