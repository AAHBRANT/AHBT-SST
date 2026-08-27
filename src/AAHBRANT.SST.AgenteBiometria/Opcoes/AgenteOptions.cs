namespace AAHBRANT.SST.AgenteBiometria.Opcoes;

public class AgenteOptions
{
    public Guid DispositivoId { get; set; }
    public string SegredoDispositivo { get; set; } = string.Empty;
    public string ChaveCriptografiaBiometriaBase64 { get; set; } = string.Empty;
    public string BackendBaseUrl { get; set; } = string.Empty;
    public string OrigemPermitida { get; set; } = string.Empty;
}
