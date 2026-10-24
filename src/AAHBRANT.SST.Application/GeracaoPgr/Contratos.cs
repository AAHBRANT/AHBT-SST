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
}

public record ContextoRiscosAtividade(
    string Atividade,
    string Ghe,
    string? Ambiente,
    IReadOnlyList<string> Funcoes,
    int MaxProbabilidade,
    int MaxSeveridade,
    IReadOnlyList<string> RequisitosLegais);
