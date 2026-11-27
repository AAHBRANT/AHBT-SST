namespace AAHBRANT.SST.Application.Common.Interfaces;

// Alertas técnicos de engenharia (saúde da assinatura) para o chat privado do responsável pelo sistema.
// O token do bot fica só no servidor: o agente do Claude Console e a vigia do Worker mandam o texto por aqui.
public interface ITelegramAlertaService
{
    // Devolve false quando o Telegram não está configurado ou recusou a mensagem (o motivo vai para o log).
    Task<bool> EnviarAsync(string mensagem, CancellationToken ct = default);
}
