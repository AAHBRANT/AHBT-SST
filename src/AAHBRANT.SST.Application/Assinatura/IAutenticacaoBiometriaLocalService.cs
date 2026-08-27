namespace AAHBRANT.SST.Application.Assinatura;

// Autenticação via biometria digital local (Futronic FS80H + agente Windows) — mesma convenção de
// "cada novo método de auth ganha sua própria interface" já usada para IAutenticacaoWebAuthnService.
// O match 1:N em si acontece no agente local (fora do backend); aqui só se reautentica o dispositivo
// (segredo compartilhado) e se confere o score que o agente já calculou contra o limiar configurado.
public interface IAutenticacaoBiometriaLocalService
{
    Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(
        Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, CancellationToken ct);
}
