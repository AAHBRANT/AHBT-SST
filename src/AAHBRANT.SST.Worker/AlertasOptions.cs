namespace AAHBRANT.SST.Worker;

// Ligado à seção "Alertas" do appsettings.json.
public class AlertasOptions
{
    public int IntervaloExecucaoMinutos { get; set; } = 60;
    public int DiasAntecedenciaVencimento { get; set; } = 30;
}
