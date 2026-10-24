namespace AAHBRANT.SST.Application.Acidentes.RelatoIa;

/// <summary>
/// Confere a base que a IA declarou para cada ação. Uma referência ao PGR ou a um requisito legal só
/// é aceita se casar com o que está de fato cadastrado — senão vira "sem base, validar". O modelo
/// às vezes associa o controle ao perigo errado (visto no teste com o PGR real da Ponte Rio Cuiá),
/// e uma referência falsa num plano de ação é pior do que nenhuma.
/// </summary>
public static class FundamentacaoAcaoPlano
{
    public record Resultado(string Texto, bool Confirmada);

    public static Resultado Conferir(
        AcaoSugeridaIa acao,
        string? atividade,
        IReadOnlyList<RiscoPgrResumo> riscos,
        IReadOnlyList<RequisitoLegalResumo> requisitos)
    {
        switch (acao.BaseOrigem)
        {
            case "PGR":
            {
                var risco = riscos.FirstOrDefault(r => Igual(r.Perigo, acao.BasePerigo));
                if (risco is null)
                    return SemBase($"a IA citou o perigo \"{acao.BasePerigo}\", que não está no PGR desta atividade");

                var controle = string.IsNullOrWhiteSpace(acao.BaseControle) ? null : acao.BaseControle.Trim();
                var texto = $"PGR · {atividade ?? "atividade"} · Perigo: {risco.Perigo}" +
                            (controle is null ? string.Empty : $" · Controle: {controle}");
                return new Resultado(Limitar(texto), true);
            }
            case "RequisitoLegal":
            {
                // A IA recebe cada requisito como "Norma Item" e deve citá-lo exatamente assim.
                var requisito = requisitos.FirstOrDefault(r => Igual(IdentificadorRequisito(r), acao.BaseReferencia));
                if (requisito is null)
                    return SemBase("a IA citou um requisito legal que não está cadastrado no sistema");

                return new Resultado(Limitar($"{requisito.Norma}{(string.IsNullOrWhiteSpace(requisito.Item) ? "" : $", item {requisito.Item}")} · {requisito.Titulo}"), true);
            }
            case "Metodo":
                return new Resultado(Limitar($"Hierarquia de medidas de prevenção (NR-01) · {Texto(acao.BaseReferencia, "método")}"), true);
            default:
                return SemBase(Texto(acao.BaseReferencia, "nenhum risco ou requisito cadastrado sustenta esta ação"));
        }
    }

    public static string IdentificadorRequisito(RequisitoLegalResumo r)
        => string.IsNullOrWhiteSpace(r.Item) ? r.Norma.Trim() : $"{r.Norma.Trim()} {r.Item.Trim()}";

    private static Resultado SemBase(string motivo) => new(Limitar($"Sem base cadastrada, validar: {motivo}"), false);

    private static bool Igual(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
           string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Texto(string? valor, string padrao) => string.IsNullOrWhiteSpace(valor) ? padrao : valor.Trim();

    private static string Limitar(string texto) => texto.Length <= 500 ? texto : texto[..500].TrimEnd();
}
