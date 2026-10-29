using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Classificação dos riscos do PGR pela tabela de cores do mapa de riscos (Portaria SSST nº 25/1994), aprovada em
// 29/10/2026. Psicossocial não existe na tabela de 1994: vai no grupo 4 com o ergonômico (NR-17, organização do trabalho).
public record GrupoRisco(int Numero, string Nome, string Cor, string CorHex, string Exemplos);

public static class GruposRisco
{
    public static readonly IReadOnlyList<GrupoRisco> Todos = new[]
    {
        new GrupoRisco(1, "Físicos", "Verde", "#2e7d32", "Ruído, vibração, calor, radiação, umidade"),
        new GrupoRisco(2, "Químicos", "Vermelho", "#c62828", "Poeiras, fumos, névoas, gases, vapores, produtos químicos"),
        new GrupoRisco(3, "Biológicos", "Marrom", "#6d4c41", "Vírus, bactérias, fungos, animais peçonhentos"),
        new GrupoRisco(4, "Ergonômicos e psicossociais", "Amarelo", "#f9a825", "Esforço físico, levantamento de peso, postura, ritmo, pressão por prazo"),
        new GrupoRisco(5, "Acidentes", "Azul", "#1565c0", "Queda, máquinas, eletricidade, atropelamento, soterramento"),
    };

    // Tipo do risco (como a IA e a tela gravam) → grupo. Nulo para tipo fora da lista, que sai sem cor.
    public static GrupoRisco? DoTipo(string? tipo) => FuncaoSstClassifier.Normalizar(tipo ?? string.Empty) switch
    {
        "fisico" => Todos[0],
        "quimico" => Todos[1],
        "biologico" => Todos[2],
        "ergonomico" or "psicossocial" => Todos[3],
        "acidente" or "mecanico" => Todos[4],
        _ => null,
    };
}
