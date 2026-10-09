namespace AAHBRANT.SST.Application.Acidentes.RelatoIa;

/// <summary>
/// Lê o relato livre de uma ocorrência (falado e transcrito, ou escrito) e sugere o preenchimento do
/// formulário de registro. É só sugestão: o técnico revisa e é quem registra.
/// </summary>
public interface IClassificadorRelatoOcorrencia
{
    Task<OcorrenciaSugeridaIa> ClassificarAsync(
        string relato,
        IReadOnlyList<RespostaPerguntaRelato> complementos,
        DateTime agoraLocal,
        IReadOnlyList<string> atividadesDaObra,
        CancellationToken ct);
}

/// <summary>Resposta do técnico a uma pergunta que a IA fez sobre o que faltava no relato.</summary>
public record RespostaPerguntaRelato(string Pergunta, string Resposta);

/// <summary>Pergunta da IA sobre uma informação que o relato não trouxe.</summary>
public record PerguntaRelatoOcorrencia(string Campo, string Pergunta);

/// <summary>Resposta bruta do modelo — enums como texto, atividade e pessoas pelo nome.</summary>
public record OcorrenciaSugeridaIa(
    string? Tipo,
    string? Gravidade,
    string? Local,
    string? Data,
    string? Hora,
    string? Descricao,
    string? Lesao,
    string? Consequencia,
    string? Atendimento,
    bool? HouveAfastamento,
    int? DiasAfastamento,
    string? Atividade,
    IReadOnlyList<string> NomesCitados,
    IReadOnlyList<PerguntaRelatoOcorrencia> Perguntas);
