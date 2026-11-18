using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Cameras;

// Situação da câmera IP mostrada na lista, calculada na consulta (sem job) a partir do último contato do agente e do
// último quadro recebido. Textos positivos, sem "vencido".
public static class SituacaoConexaoCameraRegras
{
    public const string RecebendoImagem = "Recebendo imagem";
    public const string AguardandoSinal = "Aguardando sinal";
    public const string AgenteSemContato = "Agente sem contato";

    // O agente consulta a configuração a cada ~15 s (isso é o "sinal de vida"); sem contato há mais que isto, a obra está sem agente.
    public static readonly TimeSpan LimiteSemContatoDoAgente = TimeSpan.FromMinutes(3);

    // O agente só envia quadro com movimento: câmera parada não gera quadro. Um quadro dentro desta janela conta como "recebendo".
    // Limitação conhecida (ver relatório da etapa 3): refinar com a situação por câmera que o agente informar na etapa 5.
    public static readonly TimeSpan JanelaDoUltimoQuadro = TimeSpan.FromMinutes(15);

    // Nulo = não se aplica (câmera do navegador ou desligada).
    public static string? Calcular(
        FonteCamera fonte, bool habilitada, bool agenteAtivo, DateTime? ultimoContatoDoAgenteUtc, DateTime? ultimoQuadroUtc, DateTime agoraUtc)
    {
        if (fonte != FonteCamera.Rtsp || !habilitada) return null;

        if (!agenteAtivo || ultimoContatoDoAgenteUtc is not { } contato || agoraUtc - contato > LimiteSemContatoDoAgente)
            return AgenteSemContato;

        return ultimoQuadroUtc is { } quadro && agoraUtc - quadro <= JanelaDoUltimoQuadro ? RecebendoImagem : AguardandoSinal;
    }
}
