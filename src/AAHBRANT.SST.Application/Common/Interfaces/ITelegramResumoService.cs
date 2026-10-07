namespace AAHBRANT.SST.Application.Common.Interfaces;

// Resumos operacionais para o Telegram (por exemplo, o resumo do DDS ao encerrar). Chat próprio,
// separado do Suporte IA (ITelegramSuporteService), para os resumos não se misturarem com pedidos de
// suporte. Sem token/chat configurado, simplesmente não envia.
public interface ITelegramResumoService
{
    Task EnviarAsync(string mensagem, CancellationToken ct = default);
}
