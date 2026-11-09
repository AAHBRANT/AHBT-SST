using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Cameras;

// Isola a IA do resto do módulo: cadastro, registro e alerta não sabem qual motor analisa a foto.
// Lança InvalidOperationException quando a análise não pôde ser feita ou veio incoerente.
public interface IAnalisadorEpiImagem
{
    Task<ResultadoAnaliseEpi> AnalisarAsync(byte[] imagemJpeg, CancellationToken ct);
}

public record ResultadoAnaliseEpi(int Pessoas, int SemCapacete, int SemColete, ConfiancaAnaliseEpi Confianca);
