namespace AAHBRANT.SST.Application.Common.Interfaces;

public record BotaoTelegram(string Texto, string Dado);

// Alertas técnicos de engenharia (saúde da assinatura) para o chat privado do responsável pelo sistema.
// O token do bot fica só no servidor: o agente do Claude Console e a vigia do Worker mandam o texto por aqui.
public interface ITelegramAlertaService
{
    // Devolve false quando o Telegram não está configurado ou recusou a mensagem (o motivo vai para o log).
    Task<bool> EnviarAsync(string mensagem, CancellationToken ct = default);

    // Mensagem com botões (aprovar/recusar). Devolve o id da mensagem, ou null se não foi enviada.
    Task<long?> EnviarComBotoesAsync(string mensagem, IReadOnlyList<BotaoTelegram> botoes, CancellationToken ct = default);

    // Troca o texto de uma mensagem já enviada e tira os botões.
    Task EditarAsync(long mensagemId, string mensagem, CancellationToken ct = default);

    // Fecha o "carregando" do botão no Telegram, com um aviso curto.
    Task ResponderCliqueAsync(string cliqueId, string aviso, CancellationToken ct = default);

    // Só o responsável decide: o clique precisa vir do usuário do chat de alertas.
    bool UsuarioAutorizado(long usuarioTelegramId);

    // Aponta as atualizações do bot (só cliques em botão) para a URL da API, com o segredo que o Telegram
    // devolve no cabeçalho X-Telegram-Bot-Api-Secret-Token.
    Task<bool> RegistrarWebhookAsync(string url, string segredo, CancellationToken ct = default);
}
