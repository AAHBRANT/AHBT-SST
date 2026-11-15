using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Cameras;

// Isola a IA do resto do módulo: cadastro, registro e alerta não sabem qual motor analisa a foto.
// Lança InvalidOperationException quando a análise não pôde ser feita ou veio incoerente.
public interface IAnalisadorEpiImagem
{
    Task<ResultadoAnaliseEpi> AnalisarAsync(byte[] imagemJpeg, CancellationToken ct);
}

// Retângulo da pessoa inteira em fração da imagem (0 a 1, a partir do canto superior esquerdo).
public record CaixaPessoa(double X0, double Y0, double X1, double Y1)
{
    public double Altura => Y1 - Y0;
}

// Uma pessoa vista na foto: posição aproximada do centro da cabeça em fração da imagem (0 a 1, a partir
// do canto superior esquerdo) e o que falta. Serve para ligar cada rosto reconhecido à pessoa certa
// quando há mais de uma na cena.
// SemCapacete/SemColete só valem quando o item foi AVALIADO e está não conforme (a parte do corpo aparece).
// CapaceteNaoAvaliavel/ColeteNaoAvaliavel: a parte do corpo não aparece (ou a pessoa está pequena demais),
// então o item não foi julgado e nunca gera alerta sozinho.
public record PessoaNaCena(double CabecaX, double CabecaY, bool SemCapacete, bool SemColete,
    bool CabecaVisivel = true, bool TroncoVisivel = true,
    bool CapaceteNaoAvaliavel = false, bool ColeteNaoAvaliavel = false,
    CaixaPessoa? Caixa = null,
    // Retângulo da cabeça, quando ela aparece (usado no tamanho mínimo; serve também para os quadros da Fase 4).
    CaixaPessoa? CaixaCabeca = null)
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
    UsoIa? Uso = null,
    // Pessoas cujo item não foi avaliado porque a parte do corpo não aparece. Não entram em SemCapacete/SemColete.
    int CapaceteNaoAvaliavel = 0,
    int ColeteNaoAvaliavel = 0);
