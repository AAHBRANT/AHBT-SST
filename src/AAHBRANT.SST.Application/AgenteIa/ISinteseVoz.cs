namespace AAHBRANT.SST.Application.AgenteIa;

/// <summary>
/// Lê em voz alta (texto → áudio MP3) a resposta do SST IA com voz neural. Só converte o texto que o
/// Claude já escreveu; não gera conteúdo. Sem configuração, o app usa a voz do próprio aparelho.
/// </summary>
public interface ISinteseVoz
{
    bool Configurado { get; }

    /// <summary>Devolve o áudio em MP3 (audio/mpeg).</summary>
    Task<byte[]> SintetizarAsync(string texto, CancellationToken ct);
}
