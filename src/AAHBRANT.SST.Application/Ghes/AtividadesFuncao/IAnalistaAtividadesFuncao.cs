namespace AAHBRANT.SST.Application.Ghes.AtividadesFuncao;

/// <summary>
/// Sugere, para um GHE, quais atividades cada função executa (Entrega 4 da OS). Só sugere: o servidor
/// descarta código ou função que não estava na entrada, grava como Pendente e o Engenheiro de Segurança decide.
/// </summary>
public interface IAnalistaAtividadesFuncao
{
    Task<ResultadoAnaliseAtividadesFuncao> SugerirAsync(ContextoAtividadesFuncao contexto, CancellationToken ct);
}

// Codigo é um identificador curto da atividade só para esta chamada (ex.: "A001"); o servidor converte de volta no Id.
public record AtividadeParaAnalise(string Codigo, string Nome);

public record FuncaoParaAnalise(string Nome, string? DescricaoNoPgr);

public record ContextoAtividadesFuncao(
    int GheNumero,
    string? Ambiente,
    IReadOnlyList<FuncaoParaAnalise> Funcoes,
    IReadOnlyList<AtividadeParaAnalise> Atividades);

public record SugestaoAtividadeIa(string? Codigo, string? Justificativa);

public record SugestaoFuncaoIa(string? Funcao, List<SugestaoAtividadeIa>? Atividades);

public record ResultadoAnaliseAtividadesFuncao(IReadOnlyList<SugestaoFuncaoIa> Funcoes, int TokensEntrada = 0, int TokensSaida = 0);
