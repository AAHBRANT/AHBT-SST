namespace AAHBRANT.SST.AgenteCameras.Quadros;

// Decide, a cada quadro, se ele deve ser enviado. Mesma lógica de useMonitoramentoCamera.ts:
// os 2 primeiros quadros após abrir a câmera são descartados (exposição ainda ajustando), o 3º vira só a
// referência; depois, envia quando há movimento. Acrescenta o intervalo mínimo entre envios da câmera.
public sealed class PoliticaEnvio
{
    public const int AmostrasIgnoradas = 2;

    private readonly TimeSpan _intervalo;
    private byte[]? _anterior;
    private int _amostras;
    private DateTimeOffset? _ultimoEnvio;

    public PoliticaEnvio(TimeSpan intervalo) => _intervalo = intervalo;

    /// <summary>Chamar ao reabrir o fluxo: recomeça o descarte inicial e a referência.</summary>
    public void Reiniciar()
    {
        _anterior = null;
        _amostras = 0;
    }

    public bool DeveEnviar(byte[] amostra, DateTimeOffset agora)
    {
        _amostras++;
        if (_amostras <= AmostrasIgnoradas) return false;
        if (_amostras == AmostrasIgnoradas + 1)
        {
            _anterior = amostra;
            return false;
        }

        var movimento = DetectorMovimento.HouveMovimento(_anterior, amostra);
        _anterior = amostra;
        if (!movimento) return false;
        if (_ultimoEnvio is { } ultimo && agora - ultimo < _intervalo) return false;
        _ultimoEnvio = agora;
        return true;
    }
}
