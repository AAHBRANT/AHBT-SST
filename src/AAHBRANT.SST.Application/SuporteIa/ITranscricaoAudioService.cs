namespace AAHBRANT.SST.Application.SuporteIa;

/// <summary>
/// Converte a fala do usuário em texto para a Central de Suporte IA. O áudio não é persistido:
/// vai direto ao modelo e só o texto volta, para o próprio usuário revisar antes de enviar.
/// </summary>
public interface ITranscricaoAudioService
{
    bool Configurado { get; }

    Task<string> TranscreverAsync(Stream audio, string nomeArquivo, string? contentType, CancellationToken ct);
}
