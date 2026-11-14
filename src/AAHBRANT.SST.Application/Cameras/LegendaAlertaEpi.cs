namespace AAHBRANT.SST.Application.Cameras;

// Trabalhador identificado pelo rosto numa cena com várias pessoas, e o que faltava nele.
public record IdentificadoNaCena(string Nome, bool SemCapacete, bool SemColete);

// Texto do alerta de EPI enviado como legenda da foto no Telegram (spec §6).
public static class LegendaAlertaEpi
{
    // trabalhadorNome: quem o reconhecimento facial identificou. Nulo = ninguém identificado (rosto não
    // reconhecido ou facial indisponível). Usado quando a IA viu uma única pessoa na cena.
    public static string Montar(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete,
        string? trabalhadorNome = null, int capaceteNaoAvaliavel = 0, int coleteNaoAvaliavel = 0)
        => Cabecalho(obraNome, cameraNome, momentoUtc, pessoas, semCapacete, semColete) + "\n" +
           (string.IsNullOrWhiteSpace(trabalhadorNome)
               ? "Trabalhador não identificado"
               : $"Trabalhador: {trabalhadorNome.Trim()} (identificado pelo rosto)") +
           NaoConferidos(capaceteNaoAvaliavel, coleteNaoAvaliavel);

    // Cena com várias pessoas: lista quem foi identificado pelo rosto, com o que faltava em cada um, e
    // quantos irregulares ficaram sem identificação (naoIdentificados).
    public static string MontarCena(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete,
        IReadOnlyList<IdentificadoNaCena> identificados, int naoIdentificados, int capaceteNaoAvaliavel = 0, int coleteNaoAvaliavel = 0)
    {
        var linhas = new List<string> { Cabecalho(obraNome, cameraNome, momentoUtc, pessoas, semCapacete, semColete) };

        if (identificados.Count > 0)
            linhas.Add("Identificados pelo rosto: " +
                       string.Join(", ", identificados.Select(i => $"{i.Nome.Trim()} ({Irregularidade(i.SemCapacete, i.SemColete)})")));

        if (naoIdentificados > 0)
            linhas.Add(identificados.Count == 0 && naoIdentificados == 1
                ? "Trabalhador não identificado"
                : $"Não identificados: {naoIdentificados}");

        return string.Join("\n", linhas) + NaoConferidos(capaceteNaoAvaliavel, coleteNaoAvaliavel);
    }

    // Itens que a IA não pôde julgar na mesma foto (parte do corpo fora da imagem ou pessoa pequena demais).
    // Só entram na legenda de um alerta já motivado por outro item; sozinhos nunca geram alerta.
    private static string NaoConferidos(int capaceteNaoAvaliavel, int coleteNaoAvaliavel)
    {
        var texto = string.Empty;
        if (capaceteNaoAvaliavel > 0) texto += $"\nCapacete não conferido em {capaceteNaoAvaliavel} {(capaceteNaoAvaliavel == 1 ? "pessoa" : "pessoas")}";
        if (coleteNaoAvaliavel > 0) texto += $"\nColete não conferido em {coleteNaoAvaliavel} {(coleteNaoAvaliavel == 1 ? "pessoa" : "pessoas")}";
        return texto;
    }

    private static string Cabecalho(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(momentoUtc, DateTimeKind.Utc), FusoBrasilia());

        var partes = new List<string> { $"{pessoas} {(pessoas == 1 ? "pessoa" : "pessoas")} na cena" };
        if (semCapacete > 0) partes.Add($"{semCapacete} sem capacete");
        if (semColete > 0) partes.Add($"{semColete} sem colete");

        // Nome que já começa com "Obra" (ex.: "Obra Beira Rio") não leva o prefixo de novo.
        var nomeObra = obraNome.Trim();
        var obra = nomeObra.Equals("Obra", StringComparison.OrdinalIgnoreCase) || nomeObra.StartsWith("Obra ", StringComparison.OrdinalIgnoreCase)
            ? nomeObra
            : $"Obra {nomeObra}";

        return $"⚠️ EPI — pessoa {Irregularidade(semCapacete > 0, semColete > 0)}\n" +
               $"{obra} · Câmera {cameraNome} · {local:dd/MM HH:mm}\n" +
               string.Join(", ", partes);
    }

    private static string Irregularidade(bool semCapacete, bool semColete) => (semCapacete, semColete) switch
    {
        (true, true) => "sem capacete e sem colete",
        (true, false) => "sem capacete",
        _ => "sem colete",
    };

    // Linha final da legenda de quadro que chegou atrasado (guardado sem internet no PC da obra): o horário do
    // cabeçalho já é o da captura; aqui o aviso de que o registro só aconteceu depois. Vazio quando o quadro é ao vivo.
    public static string AvisoDeAtraso(DateTime? capturadaUtc)
        => capturadaUtc is { } c
            ? $"\nRegistrado com atraso — imagem das {TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(c, DateTimeKind.Utc), FusoBrasilia()):HH:mm}"
            : string.Empty;

    private static TimeZoneInfo FusoBrasilia()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }
}
