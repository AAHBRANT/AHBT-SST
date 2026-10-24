namespace AAHBRANT.SST.Application.Assinatura;

// Autenticação via biometria digital local (Futronic FS80H + agente Windows) — mesma convenção de
// "cada novo método de auth ganha sua própria interface" já usada para IAutenticacaoWebAuthnService.
// O match 1:N em si acontece no agente local (fora do backend). Desde 11/10 o agente assina cada leitura
// (trabalhador + nota + desafio de uso único) com uma chave que só ele tem; o servidor confere essa
// assinatura em vez de confiar no que o navegador manda. O caminho antigo (segredo + nota sem assinatura)
// só vale para leitores que ainda não registraram a chave, e pode ser desligado em
// Assinatura:ExigirLeituraAssinadaBiometria.
public interface IAutenticacaoBiometriaLocalService
{
    Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(
        Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, LeituraBiometricaAssinada? leitura, CancellationToken ct);
}
