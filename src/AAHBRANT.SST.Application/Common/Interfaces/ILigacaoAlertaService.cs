namespace AAHBRANT.SST.Application.Common.Interfaces;

// Ligação de voz para o celular do responsável quando a assinatura cai (pedido do usuário, 27/11/2026):
// a mensagem do Telegram pode passar despercebida de madrugada; a ligação acorda. Só reforço — o texto
// completo continua indo pelo Telegram. Nunca leva nome, CPF nem dado de trabalhador (LGPD).
public interface ILigacaoAlertaService
{
    // false quando faltam as credenciais ou os números: a vigia simplesmente não liga.
    bool Configurada { get; }

    // Liga e lê o texto em voz alta (duas vezes). Devolve o id da chamada, ou null se não foi possível
    // (o motivo vai para o log, nunca a credencial).
    Task<string?> LigarAsync(string texto, CancellationToken ct = default);
}
