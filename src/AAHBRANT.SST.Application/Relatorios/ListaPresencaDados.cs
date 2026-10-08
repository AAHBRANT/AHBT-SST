namespace AAHBRANT.SST.Application.Relatorios;

// Horários já convertidos para Brasília (o banco guarda UTC).
public record PresencaLinha(string Nome, string? Matricula, DateTime? AssinadoEm);

public record AusenteLinha(string Nome, string? Matricula, int FaltasSeguidas);

// Dados da lista de presença de um DDS. Duração = da 1ª à última assinatura de participante (o tempo em que
// o DDS aconteceu de fato). O fechamento é mostrado à parte: pode acontecer bem depois do DDS, por causa das
// fotos de evidência.
public record ListaPresencaDados(
    Guid DdsId,
    string Obra,
    DateTime DataDds,
    string? Tema,
    DateTime GeradoEm,
    int Total,
    IReadOnlyList<PresencaLinha> Presentes,
    IReadOnlyList<AusenteLinha> Ausentes,
    DateTime? PrimeiraAssinatura,
    DateTime? UltimaAssinatura,
    int? DuracaoMinutos,
    DateTime? FechadoEm,
    int? MinutosAteFechar)
{
    public int QuantidadePresentes => Presentes.Count;
}

// Gera a imagem (PNG) da lista de presença no padrão AAHBRANT. Implementação na Infrastructure (QuestPDF).
public interface IImagemListaPresencaService
{
    byte[] Gerar(ListaPresencaDados dados);
}

public static class FusoBrasilia
{
    private static readonly TimeZoneInfo Fuso = Obter();

    public static DateTime ParaBrasilia(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Fuso);

    public static DateTime Agora() => ParaBrasilia(DateTime.UtcNow);

    public static DateTime Hoje() => Agora().Date;

    private static TimeZoneInfo Obter()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }
}
