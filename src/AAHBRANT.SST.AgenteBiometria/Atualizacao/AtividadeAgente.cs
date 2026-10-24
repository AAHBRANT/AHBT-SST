namespace AAHBRANT.SST.AgenteBiometria.Atualizacao;

// Última vez que a tela do quiosque chamou o agente. A atualização só é aplicada com o agente parado há
// alguns minutos, para nunca derrubar uma leitura de digital no meio de um DDS ou de uma entrega de EPI.
public class AtividadeAgente
{
    private long _ultimaChamadaTicks = DateTime.UtcNow.Ticks;

    public void Registrar() => Interlocked.Exchange(ref _ultimaChamadaTicks, DateTime.UtcNow.Ticks);

    public TimeSpan TempoParado => DateTime.UtcNow - new DateTime(Interlocked.Read(ref _ultimaChamadaTicks), DateTimeKind.Utc);
}
