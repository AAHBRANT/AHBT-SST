namespace AAHBRANT.SST.Application.Cameras;

// Trabalhador identificado pelo rosto numa cena com várias pessoas, e o que faltava nele.
public record IdentificadoNaCena(string Nome, bool SemCapacete, bool SemColete, bool SemOculos = false);

// Texto do alerta de EPI enviado como legenda da foto no Telegram (spec §6).
// O aviso de cor do capacete diferente da função NÃO usa esta legenda: ele nunca vai ao Telegram.
public static class LegendaAlertaEpi
{
    // trabalhadorNome: quem o reconhecimento facial identificou. Nulo = ninguém identificado (rosto não
    // reconhecido ou facial indisponível). Usado quando a IA viu uma única pessoa na cena.
    public static string Montar(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete,
        string? trabalhadorNome = null, int capaceteNaoAvaliavel = 0, int coleteNaoAvaliavel = 0,
        int semOculos = 0, int oculosNaoAvaliavel = 0)
        => Cabecalho(obraNome, cameraNome, momentoUtc, pessoas, semCapacete, semColete, semOculos) + "\n" +
           (string.IsNullOrWhiteSpace(trabalhadorNome)
               ? "Trabalhador não identificado"
               : $"Trabalhador: {trabalhadorNome.Trim()} (identificado pelo rosto)") +
           NaoConferidos(capaceteNaoAvaliavel, coleteNaoAvaliavel, oculosNaoAvaliavel);

    // Cena com várias pessoas: lista quem foi identificado pelo rosto, com o que faltava em cada um, e
    // quantos irregulares ficaram sem identificação (naoIdentificados).
    public static string MontarCena(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete,
        IReadOnlyList<IdentificadoNaCena> identificados, int naoIdentificados, int capaceteNaoAvaliavel = 0, int coleteNaoAvaliavel = 0,
        int semOculos = 0, int oculosNaoAvaliavel = 0)
    {
        var linhas = new List<string> { Cabecalho(obraNome, cameraNome, momentoUtc, pessoas, semCapacete, semColete, semOculos) };

        if (identificados.Count > 0)
            linhas.Add("Identificados pelo rosto: " +
                       string.Join(", ", identificados.Select(i => $"{i.Nome.Trim()} ({Irregularidade(i.SemCapacete, i.SemColete, i.SemOculos)})")));

        if (naoIdentificados > 0)
            linhas.Add(identificados.Count == 0 && naoIdentificados == 1
                ? "Trabalhador não identificado"
                : $"Não identificados: {naoIdentificados}");

        return string.Join("\n", linhas) + NaoConferidos(capaceteNaoAvaliavel, coleteNaoAvaliavel, oculosNaoAvaliavel);
    }

    // Itens que a IA não pôde julgar na mesma foto (parte do corpo fora da imagem ou pessoa pequena demais).
    // Só entram na legenda de um alerta já motivado por outro item; sozinhos nunca geram alerta.
    private static string NaoConferidos(int capaceteNaoAvaliavel, int coleteNaoAvaliavel, int oculosNaoAvaliavel)
    {
        var texto = string.Empty;
        if (capaceteNaoAvaliavel > 0) texto += $"\nCapacete não conferido em {capaceteNaoAvaliavel} {(capaceteNaoAvaliavel == 1 ? "pessoa" : "pessoas")}";
        if (coleteNaoAvaliavel > 0) texto += $"\nColete não conferido em {coleteNaoAvaliavel} {(coleteNaoAvaliavel == 1 ? "pessoa" : "pessoas")}";
        if (oculosNaoAvaliavel > 0) texto += $"\nÓculos não conferido em {oculosNaoAvaliavel} {(oculosNaoAvaliavel == 1 ? "pessoa" : "pessoas")}";
        return texto;
    }

    private static string Cabecalho(string obraNome, string cameraNome, DateTime momentoUtc, int pessoas, int semCapacete, int semColete, int semOculos)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(momentoUtc, DateTimeKind.Utc), FusoBrasilia());

        var partes = new List<string> { $"{pessoas} {(pessoas == 1 ? "pessoa" : "pessoas")} na cena" };
        if (semCapacete > 0) partes.Add($"{semCapacete} sem capacete");
        if (semColete > 0) partes.Add($"{semColete} sem colete");
        if (semOculos > 0) partes.Add($"{semOculos} sem óculos");

        // Nome que já começa com "Obra" (ex.: "Obra Beira Rio") não leva o prefixo de novo.
        var nomeObra = obraNome.Trim();
        var obra = nomeObra.Equals("Obra", StringComparison.OrdinalIgnoreCase) || nomeObra.StartsWith("Obra ", StringComparison.OrdinalIgnoreCase)
            ? nomeObra
            : $"Obra {nomeObra}";

        return $"⚠️ EPI — pessoa {Irregularidade(semCapacete > 0, semColete > 0, semOculos > 0)}\n" +
               $"{obra} · Câmera {cameraNome} · {local:dd/MM HH:mm}\n" +
               string.Join(", ", partes);
    }

    // "sem capacete", "sem capacete e sem colete", "sem capacete, sem colete e sem óculos"...
    private static string Irregularidade(bool semCapacete, bool semColete, bool semOculos)
    {
        var itens = new List<string>();
        if (semCapacete) itens.Add("sem capacete");
        if (semColete) itens.Add("sem colete");
        if (semOculos) itens.Add("sem óculos");
        return itens.Count switch
        {
            0 => "sem colete", // não acontece (só chega aqui com irregularidade); mantém o texto de antes
            1 => itens[0],
            _ => string.Join(", ", itens.Take(itens.Count - 1)) + " e " + itens[^1],
        };
    }

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
