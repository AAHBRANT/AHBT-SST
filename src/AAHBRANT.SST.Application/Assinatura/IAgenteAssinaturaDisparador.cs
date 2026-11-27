namespace AAHBRANT.SST.Application.Assinatura;

// Chama o agente engenheiro da assinatura (deployment do Claude Console) fora do horário agendado: a vigia
// do Worker usa quando o reconhecimento facial para de responder, para o agente investigar na hora.
public interface IAgenteAssinaturaDisparador
{
    bool Configurado { get; }

    // Devolve o id da sessão criada, ou null quando não está configurado.
    Task<string?> DispararAsync(CancellationToken ct);
}
