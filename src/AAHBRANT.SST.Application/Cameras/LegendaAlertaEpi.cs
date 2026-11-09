namespace AAHBRANT.SST.Application.Cameras;

// Texto do alerta de EPI enviado como legenda da foto no Telegram (spec §6).
public static class LegendaAlertaEpi
{
    public static string Montar(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete)
    {
        var titulo = (semCapacete > 0, semColete > 0) switch
        {
            (true, true) => "pessoa sem capacete e sem colete",
            (true, false) => "pessoa sem capacete",
            _ => "pessoa sem colete",
        };

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(momentoUtc, DateTimeKind.Utc), FusoBrasilia());

        var partes = new List<string> { $"{pessoas} {(pessoas == 1 ? "pessoa" : "pessoas")} na cena" };
        if (semCapacete > 0) partes.Add($"{semCapacete} sem capacete");
        if (semColete > 0) partes.Add($"{semColete} sem colete");

        return $"⚠️ EPI — {titulo}\n" +
               $"Obra {obraNome} · Câmera {cameraNome} · {local:dd/MM HH:mm}\n" +
               string.Join(", ", partes);
    }

    private static TimeZoneInfo FusoBrasilia()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }
}
