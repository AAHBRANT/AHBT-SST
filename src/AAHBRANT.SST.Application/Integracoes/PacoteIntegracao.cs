using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.Integracoes;

// Pacote fixo da integração em lote ("Realizar integração"): NR-06, NR-18 e NR-35, mais a NR-12 quando
// o técnico marca. Fica num lugar só porque a ficha de entrada (que deixou de agendar estes cursos), a
// dispensa de ficha por pacote válido e o fluxo em lote leem a mesma lista.
public static class PacoteIntegracao
{
    public const string Nr06 = "NR-06";
    public const string Nr18 = "NR-18";
    public const string Nr35 = "NR-35";
    public const string Nr12 = "NR-12";

    public const int CargaHorariaNr06 = 1;
    public const int CargaHorariaNr18 = 4;
    public const int CargaHorariaNr35 = 8;
    public const int CargaHorariaNr12 = 8;

    public sealed record CursoDoPacote(string Norma, int CargaHoraria, bool Opcional);

    public static IReadOnlyList<CursoDoPacote> Cursos { get; } =
    [
        new(Nr06, CargaHorariaNr06, false),
        new(Nr18, CargaHorariaNr18, false),
        new(Nr35, CargaHorariaNr35, false),
        new(Nr12, CargaHorariaNr12, true),
    ];

    // Normas que o trabalhador precisa ter válidas para dispensar a ficha na troca de função/transferência.
    public static IReadOnlyList<string> NormasObrigatorias { get; } = Cursos.Where(c => !c.Opcional).Select(c => c.Norma).ToList();

    public static bool EhNormaDoPacote(string? norma) =>
        norma is not null && Cursos.Any(c => c.Norma == norma);

    // O curso marcado AtendeNr6 conta como NR-06 mesmo que o texto da norma esteja fora do padrão.
    public static string? NormaDoCurso(CursoTreinamento curso)
    {
        var norma = CatalogoNormasRegulamentadoras.NormalizarNumero(curso.NormaReferencia);
        if (norma is null && curso.AtendeNr6) return Nr06;
        return norma;
    }

    public static bool EhCursoDoPacote(CursoTreinamento curso) =>
        curso.AtendeNr6 || EhNormaDoPacote(NormaDoCurso(curso));
}
