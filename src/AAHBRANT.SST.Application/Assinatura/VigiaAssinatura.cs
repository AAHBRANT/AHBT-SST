using AAHBRANT.SST.Application.Relatorios;

namespace AAHBRANT.SST.Application.Assinatura;

public enum DecisaoVigiaAssinatura
{
    Nada,
    // Acabou de cair: avisa no Telegram e chama o agente para investigar.
    AvisarQueda,
    // Continua fora: lembrete, sem chamar o agente de novo (ele já investigou).
    LembrarQueda,
    // Voltou: avisa que normalizou.
    AvisarVolta,
}

// Regras da vigia de 10 em 10 minutos (Worker). Só guarda o estado em memória: depois de reiniciar o
// Worker, a primeira falha vista avisa de novo — melhor um aviso repetido que um perdido.
public class VigiaAssinatura
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    // Uma resposta ruim isolada (instabilidade de rede de segundos) não acorda ninguém: confirma depois disto.
    public static readonly TimeSpan EsperaConfirmacao = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan IntervaloLembrete = TimeSpan.FromHours(2);

    private DateTime? _foraDesde;
    private DateTime? _ultimoAviso;

    public bool Fora => _foraDesde is not null;
    public DateTime? ForaDesdeUtc => _foraDesde;

    // resultadoConfirmado: o resultado da segunda verificação, quando a primeira falhou.
    public DecisaoVigiaAssinatura Avaliar(VerificacaoServicoFacialDto resultadoConfirmado, DateTime agoraUtc)
    {
        if (resultadoConfirmado.Ok)
        {
            if (_foraDesde is null)
                return DecisaoVigiaAssinatura.Nada;
            _foraDesde = null;
            _ultimoAviso = null;
            return DecisaoVigiaAssinatura.AvisarVolta;
        }

        if (_foraDesde is null)
        {
            _foraDesde = agoraUtc;
            _ultimoAviso = agoraUtc;
            return DecisaoVigiaAssinatura.AvisarQueda;
        }

        if (_ultimoAviso is { } ultimo && agoraUtc - ultimo < IntervaloLembrete)
            return DecisaoVigiaAssinatura.Nada;
        _ultimoAviso = agoraUtc;
        return DecisaoVigiaAssinatura.LembrarQueda;
    }

    public string Mensagem(DecisaoVigiaAssinatura decisao, VerificacaoServicoFacialDto resultado, DateTime agoraUtc, bool agenteChamado)
    {
        var hora = FusoBrasilia.ParaBrasilia(agoraUtc).ToString("dd/MM HH:mm");
        var detalhe = $"{resultado.Endpoint}: {resultado.StatusHttp?.ToString() ?? "sem resposta"} {resultado.CodigoErro} {resultado.Mensagem}".Trim();
        return decisao switch
        {
            DecisaoVigiaAssinatura.AvisarQueda =>
                $"🔴 Reconhecimento facial FORA ({hora}).\n{detalhe}\n\nToda assinatura pelo rosto vai falhar: as obras devem usar a digital.\n" +
                (agenteChamado ? "O agente engenheiro foi chamado para investigar e manda o diagnóstico aqui." : "Agente engenheiro não configurado: investigar manualmente."),
            DecisaoVigiaAssinatura.LembrarQueda =>
                $"🔴 Reconhecimento facial continua FORA desde {FusoBrasilia.ParaBrasilia(_foraDesde ?? agoraUtc):dd/MM HH:mm} ({hora}).\n{detalhe}",
            DecisaoVigiaAssinatura.AvisarVolta =>
                $"🟢 Reconhecimento facial voltou a responder ({hora}).",
            _ => string.Empty,
        };
    }
}
