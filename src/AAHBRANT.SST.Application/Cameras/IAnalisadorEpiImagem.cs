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

// Tokens que a IA cobrou por uma análise (entrada = foto + instruções; saída = resposta e raciocínio).
public record UsoIa(int TokensEntrada, int TokensSaida);

// Preço do modelo das câmeras, para transformar tokens em custo. Claude Haiku 5.5, tabela da Anthropic de
// 06/10/2026 (prompts de até 100 mil tokens): US$ 0,10 por milhão de entrada e US$ 0,50 por milhão de saída.
// Trocar aqui se Claude:ModeloCameras mudar de modelo.
public static class CustoIaCameras
{
    public const decimal DolaresPorMilhaoEntrada = 0.10m;
    public const decimal DolaresPorMilhaoSaida = 0.50m;

    public static decimal EmDolares(long tokensEntrada, long tokensSaida)
        => Math.Round((tokensEntrada * DolaresPorMilhaoEntrada + tokensSaida * DolaresPorMilhaoSaida) / 1_000_000m, 4);
}

// Detalhe: uma entrada por pessoa visível. Nulo quando a IA não informou posições coerentes com a contagem;
// nesse caso a cena com várias pessoas fica sem identificação por rosto.
public record ResultadoAnaliseEpi(int Pessoas, int SemCapacete, int SemColete, ConfiancaAnaliseEpi Confianca,
    IReadOnlyList<PessoaNaCena>? Detalhe = null,
    // Nulo quando o motor não informa o consumo.
    UsoIa? Uso = null);
