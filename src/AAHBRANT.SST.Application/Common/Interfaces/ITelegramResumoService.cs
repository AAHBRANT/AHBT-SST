namespace AAHBRANT.SST.Application.Common.Interfaces;

// Resumos operacionais para o Telegram (por exemplo, o resumo do DDS ao encerrar). Por padrão vai para o
// mesmo grupo do Suporte IA (ITelegramSuporteService); Telegram:ResumoChatId permite separar. Sem
// token/chat configurado, simplesmente não envia.
public interface ITelegramResumoService
{
    Task EnviarAsync(string mensagem, CancellationToken ct = default);
}
