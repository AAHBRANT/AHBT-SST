using Anthropic;
using Anthropic.Models.Beta.Deployments;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.AgenteIa;

public class AgenteAssinaturaOptions
{
    // Id do deployment agendado do agente no Claude Console (depl_...). Vazio = a vigia só avisa no Telegram.
    public string? DeploymentId { get; set; }

    // Liga a vigia de 10 em 10 minutos no Worker.
    public bool VigiaHabilitada { get; set; }
}

// Execução manual do deployment (POST /v1/deployments/{id}/run): cria uma sessão na hora, com os mesmos
// agente, ambiente, cofre e mensagem inicial do agendamento das 6h30.
public sealed class AnthropicAgenteAssinaturaDisparador : IAgenteAssinaturaDisparador
{
    private readonly ClaudeOptions _claude;
    private readonly AgenteAssinaturaOptions _opcoes;
    private readonly Lazy<AnthropicClient> _cliente;

    public AnthropicAgenteAssinaturaDisparador(IOptions<ClaudeOptions> claude, IOptions<AgenteAssinaturaOptions> opcoes)
    {
        _claude = claude.Value;
        _opcoes = opcoes.Value;
        _cliente = new Lazy<AnthropicClient>(() => new AnthropicClient { ApiKey = _claude.ApiKey });
    }

    public bool Configurado => !string.IsNullOrWhiteSpace(_claude.ApiKey) && !string.IsNullOrWhiteSpace(_opcoes.DeploymentId);

    public async Task<string?> DispararAsync(CancellationToken ct)
    {
        if (!Configurado)
            return null;
        var execucao = await _cliente.Value.Beta.Deployments.Run(_opcoes.DeploymentId!, new DeploymentRunParams
        {
            WorkspaceID = string.IsNullOrWhiteSpace(_claude.WorkspaceId) ? null : _claude.WorkspaceId,
        }, ct);
        return execucao.SessionID;
    }
}
