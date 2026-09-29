namespace AAHBRANT.SST.AgenteBiometria.Opcoes;

public class AgenteOptions
{
    public Guid DispositivoId { get; set; }
    public string SegredoDispositivo { get; set; } = string.Empty;
    public string ChaveCriptografiaBiometriaBase64 { get; set; } = string.Empty;
    public string BackendBaseUrl { get; set; } = string.Empty;
    public string OrigemPermitida { get; set; } = string.Empty;

    // "Simulado" (padrão, sem hardware) ou "Futronic" (leitor real via ftrScanAPI.dll, exige processo x86).
    public string Leitor { get; set; } = "Simulado";

    // Ajuste de polaridade da imagem para o SourceAFIS (espera cristas escuras em fundo claro).
    public bool InverterImagem { get; set; }

    // De quanto em quanto tempo o agente busca os templates no backend (0 desliga).
    public int IntervaloSincronizacaoMinutos { get; set; } = 2;

    // Liga a detecção de dedo vivo (LFD) do leitor. Desligada por padrão: no FS80H testado ela recusou a
    // maioria dos dedos verdadeiros. Ver docs/piloto-leitor-biometrico.md.
    public bool DetectarDedoFalso { get; set; }

    // Tempo que o agente espera o dedo em cada leitura (segundos). Aumente se os trabalhadores precisarem de mais tempo.
    public int TempoLimiteDedoSegundos { get; set; } = 15;
}
