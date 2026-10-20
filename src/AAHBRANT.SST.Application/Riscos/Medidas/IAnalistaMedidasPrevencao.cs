namespace AAHBRANT.SST.Application.Riscos.Medidas;

/// <summary>
/// Separa o texto de controles do PGR em medidas por fase do trabalho (antes, durante, após) e
/// sugere as que faltam. Cada medida vem marcada com a origem que a IA declarou; a conferência de que
/// uma medida "do PGR" está mesmo no texto é feita no servidor (ConciliacaoMedidasPgr).
/// </summary>
public interface IAnalistaMedidasPrevencao
{
    Task<IReadOnlyList<MedidaSugeridaIa>> SepararAsync(ContextoMedidasRisco contexto, CancellationToken ct);
}

public record ContextoMedidasRisco(
    string Atividade,
    string Perigo,
    string? Consequencia,
    string Nivel,
    string? TextoPgr,
    IReadOnlyList<string> RequisitosLegais);

// Fase: "Antes" | "Durante" | "Apos". Origem: "PGR" (TrechoPgr = trecho copiado do texto) | "Sugestao".
public record MedidaSugeridaIa(string? Fase, string? Texto, string? Origem, string? TrechoPgr);
