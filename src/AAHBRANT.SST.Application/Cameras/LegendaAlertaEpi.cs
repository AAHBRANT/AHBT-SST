namespace AAHBRANT.SST.Application.Cameras;

// Trabalhador identificado pelo rosto numa cena com várias pessoas, e o que faltava nele.
public record IdentificadoNaCena(string Nome, bool SemCapacete, bool SemColete);

// Texto do alerta de EPI enviado como legenda da foto no Telegram (spec §6).
public static class LegendaAlertaEpi
{
    // trabalhadorNome: quem o reconhecimento facial identificou. Nulo = ninguém identificado (rosto não
    // reconhecido ou facial indisponível). Usado quando a IA viu uma única pessoa na cena.
    public static string Montar(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete,
        string? trabalhadorNome = null)
        => Cabecalho(obraNome, cameraNome, momentoUtc, pessoas, semCapacete, semColete) + "\n" +
           (string.IsNullOrWhiteSpace(trabalhadorNome)
               ? "Trabalhador não identificado"
               : $"Trabalhador: {trabalhadorNome.Trim()} (identificado pelo rosto)");

    // Cena com várias pessoas: lista quem foi identificado pelo rosto, com o que faltava em cada um, e
    // quantos irregulares ficaram sem identificação (naoIdentificados).
    public static string MontarCena(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete,
        IReadOnlyList<IdentificadoNaCena> identificados, int naoIdentificados)
    {
        var linhas = new List<string> { Cabecalho(obraNome, cameraNome, momentoUtc, pessoas, semCapacete, semColete) };

        if (identificados.Count > 0)
            linhas.Add("Identificados pelo rosto: " +
                       string.Join(", ", identificados.Select(i => $"{i.Nome.Trim()} ({Irregularidade(i.SemCapacete, i.SemColete)})")));

        if (naoIdentificados > 0)
            linhas.Add(identificados.Count == 0 && naoIdentificados == 1
                ? "Trabalhador não identificado"
                : $"Não identificados: {naoIdentificados}");

        return string.Join("\n", linhas);
    }

    private static string Cabecalho(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(momentoUtc, DateTimeKind.Utc), FusoBrasilia());

        var partes = new List<string> { $"{pessoas} {(pessoas == 1 ? "pessoa" : "pessoas")} na cena" };
        if (semCapacete > 0) partes.Add($"{semCapacete} sem capacete");
        if (semColete > 0) partes.Add($"{semColete} sem colete");

        return $"⚠️ EPI — pessoa {Irregularidade(semCapacete > 0, semColete > 0)}\n" +
               $"Obra {obraNome} · Câmera {cameraNome} · {local:dd/MM HH:mm}\n" +
               string.Join(", ", partes);
    }

    private static string Irregularidade(bool semCapacete, bool semColete) => (semCapacete, semColete) switch
    {
        (true, true) => "sem capacete e sem colete",
        (true, false) => "sem capacete",
        _ => "sem colete",
    };

    private static TimeZoneInfo FusoBrasilia()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }
}
