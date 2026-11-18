namespace AAHBRANT.SST.AgenteCameras.Quadros;

public static class EsperaCrescente
{
    public static readonly TimeSpan Maxima = TimeSpan.FromSeconds(60);

    /// <summary>Tentativa 1 = 2 s, 2 = 4 s, 3 = 8 s... até 60 s. Nunca desiste.</summary>
    public static TimeSpan Calcular(int tentativa)
    {
        if (tentativa < 1) tentativa = 1;
        if (tentativa >= 6) return Maxima;
        return TimeSpan.FromSeconds(Math.Pow(2, tentativa));
    }
}
