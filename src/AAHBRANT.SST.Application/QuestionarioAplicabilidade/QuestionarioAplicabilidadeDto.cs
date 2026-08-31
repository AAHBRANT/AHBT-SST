namespace AAHBRANT.SST.Application.QuestionarioAplicabilidade;

public record ItemQuestionarioAplicabilidadeDto(Guid Id, string Pergunta, string? TextoApoio);

// Resposta é nullable: a obra pode ainda não ter respondido este item — mesmo estado que o Motor de
// Aplicabilidade (Fase 2) vai ler como "Em análise" para qualquer requisito que dependa dele.
public record RespostaQuestionarioObraDto(
    Guid ItemId,
    string Pergunta,
    string? TextoApoio,
    bool? Resposta,
    string? Observacao);
