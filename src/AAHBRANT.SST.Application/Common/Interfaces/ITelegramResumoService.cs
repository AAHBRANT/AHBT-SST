namespace AAHBRANT.SST.Application.Common.Interfaces;

// Resumos operacionais para o Telegram (por exemplo, o resumo do DDS ao encerrar). Por padrão vai para o
// mesmo grupo do Suporte IA (ITelegramSuporteService); Telegram:ResumoChatId permite separar. Sem
// token/chat configurado, simplesmente não envia.
public interface ITelegramResumoService
{
    Task EnviarAsync(string mensagem, CancellationToken ct = default);

    // Envia uma imagem (PNG) com legenda curta. Devolve false quando não enviou (sem configuração ou o
    // Telegram recusou), para o chamador cair para o texto.
    Task<bool> EnviarImagemAsync(byte[] imagemPng, string legenda, CancellationToken ct = default);

    // Envia um arquivo (por exemplo, o PDF de detalhe do relatório). Devolve false quando não enviou.
    Task<bool> EnviarDocumentoAsync(byte[] arquivo, string nomeArquivo, string legenda, CancellationToken ct = default);
}
