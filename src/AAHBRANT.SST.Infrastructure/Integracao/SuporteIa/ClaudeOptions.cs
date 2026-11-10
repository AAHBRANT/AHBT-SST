namespace AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

/// <summary>
/// API do Claude (Anthropic). Quando ApiKey está preenchida, as chamadas de chat com saída JSON
/// vão para o Claude em vez do Azure OpenAI. A transcrição de áudio continua no Azure.
/// </summary>
public class ClaudeOptions
{
    public string? ApiKey { get; set; }
    /// <summary>
    /// ID do workspace da Anthropic. Obrigatório quando a chave não está vinculada a um workspace
    /// (sem o header anthropic-workspace-id o Claude responde 400).
    /// </summary>
    public string? WorkspaceId { get; set; }
    public string Modelo { get; set; } = "claude-sonnet-5-5";
    /// <summary>
    /// Modelo da análise de EPI por imagem (módulo Câmeras). A análise roda sem parar enquanto há movimento,
    /// então usa um modelo bem mais barato por foto que o das demais leituras (decisão do usuário, 09/11/2026).
    /// </summary>
    public string ModeloCameras { get; set; } = "claude-haiku-5-5";
    public string Endpoint { get; set; } = "https://api.anthropic.com";
}
