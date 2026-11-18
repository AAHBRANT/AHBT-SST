using AAHBRANT.SST.AgenteCameras.Opcoes;

namespace AAHBRANT.SST.AgenteCameras.Quadros;

public interface IFonteQuadros
{
    /// <summary>
    /// Abre o fluxo da câmera e entrega um JPEG por quadro (cerca de 1 por segundo). Termina quando o fluxo
    /// acaba (a leitura encerrou) ou lança exceção em caso de erro. Cancelar o token encerra o fluxo e o
    /// processo filho, se houver.
    /// </summary>
    IAsyncEnumerable<byte[]> LerQuadrosAsync(CameraConfig camera, CancellationToken ct);
}

/// <summary>Parâmetros de captura do ffmpeg: quadros por segundo, largura máxima e qualidade JPEG (-q:v, 2 melhor a 31 pior).</summary>
public sealed record PerfilCaptura(int Fps, int LarguraMaxima, int QualidadeJpeg)
{
    /// <summary>Valores da análise de EPI (não mudar: o comportamento atual depende deles).</summary>
    public static readonly PerfilCaptura Analise = new(1, 1280, 4);

    public const int QualidadeAoVivo = 7;

    public static PerfilCaptura AoVivo(int fps, int larguraMaxima) => new(fps, larguraMaxima, QualidadeAoVivo);
}

/// <summary>Fonte de quadros com perfil próprio (transmissão "quase ao vivo"). Mesmo contrato de cancelamento da IFonteQuadros.</summary>
public interface IFonteAoVivo
{
    IAsyncEnumerable<byte[]> LerQuadrosAsync(CameraConfig camera, PerfilCaptura perfil, CancellationToken ct);
}

public sealed class FfmpegIndisponivelException : Exception
{
    public FfmpegIndisponivelException(string message) : base(message) { }
}

public sealed class FonteEncerradaException : Exception
{
    public FonteEncerradaException(string message) : base(message) { }
}
