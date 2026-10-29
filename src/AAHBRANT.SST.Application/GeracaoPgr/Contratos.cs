namespace AAHBRANT.SST.Application.GeracaoPgr;

// Texto do arquivo da EAP, uma linha por item (Excel: uma linha por linha da planilha; MS Project XML:
// uma linha por tarefa com nível e datas; PDF: texto por página). Implementado em Infrastructure.
public interface IExtratorTextoEap
{
    // Lança InvalidOperationException com mensagem para o usuário quando o formato não é suportado.
    string Extrair(byte[] conteudo, string nomeArquivo, string contentType);
}

// Leitura da EAP e geração do PGR com IA. Tudo volta como sugestão: o engenheiro revisa e aprova.
public interface IGeradorPgrIa
{
    Task<List<ItemEapGerado>> LerEapAsync(string textoEap, CancellationToken ct);

    Task<List<GheGerado>> SugerirGhesAsync(IReadOnlyList<ItemEapGerado> atividades, IReadOnlyList<string> funcoesCadastradas, CancellationToken ct);

    Task<List<RiscoGerado>> GerarRiscosAsync(ContextoRiscosAtividade contexto, CancellationToken ct);

    // Escolhe, entre os locais reais que o mapa devolveu, o mais adequado a cada tipo e explica o caminho.
    Task<SugestaoEmergenciaIa> SugerirEmergenciaAsync(ContextoEmergenciaIa contexto, CancellationToken ct);

    // Forma de acompanhamento, periodicidade e critério de aferição para cada ação do plano de ação. Só sugestão.
    Task<List<AcompanhamentoSugeridoIa>> SugerirAcompanhamentoAsync(ContextoAcompanhamentoPlano contexto, CancellationToken ct);

    // CBO de cada função, escolhido entre os candidatos da tabela oficial que o servidor mandou. Só sugestão.
    Task<List<CboSugeridoIa>> SugerirCboAsync(IReadOnlyList<FuncaoParaCbo> funcoes, CancellationToken ct);
}

public record CandidatoCbo(string Codigo, string Titulo, string? Sinonimo);

// CboAtual: código que a função já tem (do cadastro ou escolhido antes). A IA mantém, salvo se outro candidato
// corresponder claramente melhor às atividades descritas.
public record FuncaoParaCbo(string Nome, string? Descricao, IReadOnlyList<CandidatoCbo> Candidatos, string? CboAtual = null);

// Codigo nulo = nenhum candidato serve; quem aplica só aceita código que estava entre os candidatos da função.
public record CboSugeridoIa(string Funcao, string? Codigo);

public record AcaoParaAcompanhamento(int Indice, string Descricao, DateTime? Prazo);

public record ContextoAcompanhamentoPlano(string Obra, IReadOnlyList<AcaoParaAcompanhamento> Acoes, IReadOnlyList<string> Riscos);

// Indice é o da ação na lista enviada; Forma e Periodicidade chegam como a IA escreveu e são normalizadas por quem aplica.
public record AcompanhamentoSugeridoIa(int Indice, string? Forma, string? Periodicidade, string? Criterio);

public record ContextoRiscosAtividade(
    string Atividade,
    string Ghe,
    string? Ambiente,
    IReadOnlyList<string> Funcoes,
    int MaxProbabilidade,
    int MaxSeveridade,
    IReadOnlyList<string> RequisitosLegais);
