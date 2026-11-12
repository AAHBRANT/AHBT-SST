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

public sealed class FfmpegIndisponivelException : Exception
{
    public FfmpegIndisponivelException(string message) : base(message) { }
}

public sealed class FonteEncerradaException : Exception
{
    public FonteEncerradaException(string message) : base(message) { }
}
