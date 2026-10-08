namespace AAHBRANT.SST.Application.Common.Interfaces;

// Canal de captura do Banco de Ideias (especificação §2/§4). O Telegram NÃO é o banco de dados (§18):
// este serviço só responde no chat e baixa anexos enviados pelo usuário.
public interface ITelegramIdeiasService
{
    // true quando o chat está na lista permitida (Telegram:IdeiasChatIds). Sem lista = nenhum chat aceito.
    bool ChatPermitido(long chatId);

    // Valida o cabeçalho X-Telegram-Bot-Api-Secret-Token contra Telegram:IdeiasWebhookSecret (falha fechada).
    bool SegredoValido(string? segredoRecebido);

    // Envia texto; devolve o id da mensagem enviada (para ligar perguntas pendentes) ou null se não enviou.
    Task<long?> EnviarMensagemAsync(long chatId, string texto, long? respondeMensagemId, CancellationToken ct = default);

    // Baixa um arquivo pelo file_id; null se falhar ou exceder maxBytes.
    Task<byte[]?> BaixarArquivoAsync(string fileId, int maxBytes, CancellationToken ct = default);
}
