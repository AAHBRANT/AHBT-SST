namespace AAHBRANT.SST.Application.Common.Interfaces;

public interface ITelegramService
{
    string ObterNomeUsuarioBot();

    // callbackData, quando informado, anexa um botão inline "Confirmo ciência" à mensagem
    // (usado pela confirmação de ciência do DDS). Retorna o message_id do Telegram, necessário
    // para editar a legenda depois (marcar "confirmado").
    Task<int?> EnviarDocumentoAsync(
        long chatId,
        string nomeArquivo,
        byte[] conteudo,
        string? legenda,
        string? callbackData,
        CancellationToken ct);

    Task ResponderCallbackAsync(string callbackQueryId, string texto, CancellationToken ct);

    Task EditarLegendaAsync(long chatId, int messageId, string legenda, CancellationToken ct);
}
