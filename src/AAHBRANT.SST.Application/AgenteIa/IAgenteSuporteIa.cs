namespace AAHBRANT.SST.Application.AgenteIa;

/// <summary>
/// Agente de suporte do G-SST (Claude Managed Agents). Uma sessão por conversa da bola flutuante.
/// O agente não acessa dados do sistema: só orienta o uso das telas (ver docs/agente-suporte).
/// </summary>
public interface IAgenteSuporteIa
{
    bool Configurado { get; }

    /// <summary>Abre uma sessão nova ligada ao usuário (chave guardada no metadata da sessão).</summary>
    Task<string> CriarSessaoAsync(string chaveUsuario, CancellationToken ct);

    /// <summary>true quando a sessão existe e pertence ao usuário.</summary>
    Task<bool> SessaoPertenceAsync(string sessaoId, string chaveUsuario, CancellationToken ct);

    /// <summary>
    /// Envia a mensagem e devolve os eventos da resposta até o agente parar (fim ou erro).
    /// </summary>
    IAsyncEnumerable<AgenteIaEvento> EnviarAsync(string sessaoId, string texto, CancellationToken ct);
}

/// <summary>
/// Evento repassado ao navegador.
/// Tipo: "delta" (pedaço de texto em geração, Id = mensagem), "mensagem" (texto final da mensagem Id,
/// substitui os deltas), "status" (o agente está pensando/consultando), "fim" e "erro".
/// </summary>
public record AgenteIaEvento(string Tipo, string? Id = null, string? Texto = null);
