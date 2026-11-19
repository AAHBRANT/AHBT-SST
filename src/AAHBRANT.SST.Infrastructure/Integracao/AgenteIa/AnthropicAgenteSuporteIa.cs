using System.Runtime.CompilerServices;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Sessions;
using Anthropic.Models.Beta.Sessions.Events;
using AAHBRANT.SST.Application.AgenteIa;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.AgenteIa;

/// <summary>
/// Agente de suporte pelo SDK oficial da Anthropic (Managed Agents, beta). Fluxo por mensagem:
/// abre o stream da sessão, envia o user.message e repassa os eventos até o session.status_idle.
/// </summary>
public sealed class AnthropicAgenteSuporteIa : IAgenteSuporteIa
{
    private const string ChaveMetadataUsuario = "gsst_usuario";

    private readonly ClaudeOptions _opcoes;
    private readonly ILogger<AnthropicAgenteSuporteIa> _logger;
    private readonly Lazy<AnthropicClient> _cliente;

    public AnthropicAgenteSuporteIa(IOptions<ClaudeOptions> opcoes, ILogger<AnthropicAgenteSuporteIa> logger)
    {
        _opcoes = opcoes.Value;
        _logger = logger;
        _cliente = new Lazy<AnthropicClient>(() => new AnthropicClient { ApiKey = _opcoes.ApiKey });
    }

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(_opcoes.ApiKey)
        && !string.IsNullOrWhiteSpace(_opcoes.AgenteSuporteId)
        && !string.IsNullOrWhiteSpace(_opcoes.AgenteSuporteAmbienteId);

    private string? Workspace => string.IsNullOrWhiteSpace(_opcoes.WorkspaceId) ? null : _opcoes.WorkspaceId;

    public async Task<string> CriarSessaoAsync(string chaveUsuario, CancellationToken ct)
    {
        var sessao = await _cliente.Value.Beta.Sessions.Create(new SessionCreateParams
        {
            Agent = _opcoes.AgenteSuporteId!,
            EnvironmentID = _opcoes.AgenteSuporteAmbienteId!,
            Title = "Bola de suporte G-SST",
            Metadata = new Dictionary<string, string> { [ChaveMetadataUsuario] = chaveUsuario },
            WorkspaceID = Workspace,
        }, ct);
        return sessao.ID;
    }

    public async Task<bool> SessaoPertenceAsync(string sessaoId, string chaveUsuario, CancellationToken ct)
    {
        try
        {
            var sessao = await _cliente.Value.Beta.Sessions.Retrieve(sessaoId,
                new SessionRetrieveParams { WorkspaceID = Workspace }, ct);
            return sessao.Metadata is not null
                && sessao.Metadata.TryGetValue(ChaveMetadataUsuario, out var dono)
                && dono == chaveUsuario;
        }
        catch (AnthropicNotFoundException)
        {
            return false;
        }
    }

    public async IAsyncEnumerable<AgenteIaEvento> EnviarAsync(string sessaoId, string texto,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var eventos = System.Threading.Channels.Channel.CreateUnbounded<AgenteIaEvento>();

        // O yield não pode ficar dentro de try/catch; a leitura do stream roda numa tarefa que
        // escreve no canal, e os erros viram um evento "erro" para o navegador.
        var leitura = Task.Run(async () =>
        {
            try
            {
                await LerRespostaAsync(sessaoId, texto, eventos.Writer, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Usuário fechou a bola ou saiu da página.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no agente de suporte (sessão {SessaoId})", sessaoId);
                eventos.Writer.TryWrite(new AgenteIaEvento("erro", Texto: MensagemErro(ex)));
            }
            finally
            {
                eventos.Writer.TryComplete();
            }
        }, ct);

        await foreach (var evento in eventos.Reader.ReadAllAsync(ct))
            yield return evento;

        await leitura;
    }

    private async Task LerRespostaAsync(string sessaoId, string texto,
        System.Threading.Channels.ChannelWriter<AgenteIaEvento> saida, CancellationToken ct)
    {
        var cliente = _cliente.Value;

        // Stream aberto ANTES do envio, senão os primeiros eventos se perdem.
        var stream = cliente.Beta.Sessions.Events.StreamStreaming(sessaoId, new EventStreamParams
        {
            WorkspaceID = Workspace,
            EventDeltas = ["agent.message"],
        }, ct);

        await cliente.Beta.Sessions.Events.Send(sessaoId, new EventSendParams
        {
            WorkspaceID = Workspace,
            Events = [new BetaManagedAgentsUserMessageEventParams
            {
                Content = [new BetaManagedAgentsTextBlock { Text = texto }],
            }],
        }, ct);

        await foreach (var ev in stream.WithCancellation(ct))
        {
            if (ev.TryPickDeltaEvent(out var delta))
            {
                await saida.WriteAsync(new AgenteIaEvento("delta", delta.EventID, delta.Delta.Content.Text), ct);
            }
            else if (ev.TryPickAgentMessageEvent(out var mensagem))
            {
                var partes = new List<string>();
                foreach (var bloco in mensagem.Content)
                    if (bloco.TryPickBetaManagedAgentsTextBlock(out var t)) partes.Add(t.Text);
                await saida.WriteAsync(new AgenteIaEvento("mensagem", mensagem.ID, string.Concat(partes)), ct);
            }
            else if (ev.TryPickAgentThinkingEvent(out _))
            {
                await saida.WriteAsync(new AgenteIaEvento("status", Texto: "Pensando…"), ct);
            }
            else if (ev.TryPickAgentToolUseEvent(out _) || ev.TryPickAgentMcpToolUseEvent(out _))
            {
                await saida.WriteAsync(new AgenteIaEvento("status", Texto: "Consultando…"), ct);
            }
            else if (ev.TryPickSessionErrorEvent(out var erro))
            {
                _logger.LogWarning("Agente de suporte: session.error na sessão {SessaoId}: {Erro}", sessaoId, erro);
                await saida.WriteAsync(new AgenteIaEvento("erro", Texto: "O assistente encontrou um problema. Tente de novo em instantes."), ct);
                return;
            }
            else if (ev.TryPickSessionStatusTerminatedEvent(out _))
            {
                await saida.WriteAsync(new AgenteIaEvento("fim", Texto: "encerrada"), ct);
                return;
            }
            else if (ev.TryPickSessionStatusIdleEvent(out var idle))
            {
                var motivo = idle.StopReason.Type.Raw();
                if (idle.StopReason.Type == StopReasonType.EndTurn)
                {
                    await saida.WriteAsync(new AgenteIaEvento("fim"), ct);
                }
                else
                {
                    // requires_action (ferramenta que pede confirmação) não é usado por este agente;
                    // retries_exhausted e budget_reached encerram a resposta com aviso.
                    _logger.LogWarning("Agente de suporte parou com {Motivo} (sessão {SessaoId})", motivo, sessaoId);
                    await saida.WriteAsync(new AgenteIaEvento("erro", Texto: "O assistente não conseguiu concluir a resposta. Tente de novo."), ct);
                }
                return;
            }
        }
    }

    private static string MensagemErro(Exception ex) => ex switch
    {
        AnthropicRateLimitException => "Muitas perguntas ao mesmo tempo. Aguarde um minuto e tente de novo.",
        AnthropicNotFoundException => "A conversa expirou. Comece uma nova conversa.",
        _ => "Não foi possível falar com o assistente agora. Tente de novo em instantes.",
    };
}
