namespace AAHBRANT.SST.Application.Ideias;

// Especificação §3 e §8 — a IA interpreta a mensagem e apoia a análise, mas nunca altera a mensagem
// original nem decide. Hoje só existe a implementação heurística (HeuristicaIdeiaEstruturacaoService),
// no mesmo espírito do Suporte IA; um provedor de LLM pode ser plugado trocando o registro em DI.
public interface IIdeiaEstruturacaoService
{
    Task<IdeiaEstruturada> EstruturarAsync(string mensagem, CancellationToken ct);
}

public record IdeiaEstruturada(
    string Titulo,
    string Descricao,
    string? ProblemaOportunidade,
    string? Objetivo,
    string? SolucaoSugerida,
    string? Modulo,
    string? Categoria,
    string? BeneficioEsperado,
    string? PossiveisImpactos,
    string? IntegracoesNecessarias,
    bool? NecessidadeIa,
    string? InformacoesFaltantes,
    string EstruturadoPor);
