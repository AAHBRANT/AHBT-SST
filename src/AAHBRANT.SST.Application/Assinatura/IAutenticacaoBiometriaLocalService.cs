namespace AAHBRANT.SST.Application.Assinatura;

// Autenticação via biometria digital local (Futronic FS80H + agente Windows) — mesma convenção de
// "cada novo método de auth ganha sua própria interface" já usada para IAutenticacaoWebAuthnService.
// O match 1:N em si acontece no agente local (fora do backend). Desde 11/10 o agente assina cada leitura
// (trabalhador + nota + desafio de uso único) com uma chave que só ele tem; o servidor confere essa
// assinatura em vez de confiar no que o navegador manda. O caminho antigo (segredo + nota sem assinatura)
// só vale para leitores que ainda não registraram a chave, e pode ser desligado em
// Assinatura:ExigirLeituraAssinadaBiometria.
//
// permitirInstrutorDeOutraObra: só a assinatura do instrutor da turma passa true. Aceita um trabalhador de
// outra obra se ele tiver perfil de instrutor autorizado a assinar em outras obras (a digital dele é
// sincronizada nos leitores delas). Qualquer outro fluxo continua restrito à obra do leitor.
public interface IAutenticacaoBiometriaLocalService
{
    Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(
        Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, LeituraBiometricaAssinada? leitura, CancellationToken ct,
        bool permitirInstrutorDeOutraObra = false);
}
