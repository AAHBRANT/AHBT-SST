namespace AAHBRANT.SST.Application.Cameras;

// Texto do alerta de EPI enviado como legenda da foto no Telegram (spec §6).
public static class LegendaAlertaEpi
{
    // trabalhadorNome: quem o reconhecimento facial identificou. Nulo = ninguém identificado (rosto não
    // reconhecido, mais de uma pessoa na cena ou facial indisponível).
    public static string Montar(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete,
        string? trabalhadorNome = null)
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
               string.Join(", ", partes) + "\n" +
               (string.IsNullOrWhiteSpace(trabalhadorNome)
                   ? "Trabalhador não identificado"
                   : $"Trabalhador: {trabalhadorNome.Trim()} (identificado pelo rosto)");
    }

    private static TimeZoneInfo FusoBrasilia()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }
}
