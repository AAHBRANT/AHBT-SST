using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Assinatura;

public record ResultadoAutenticacaoAssinatura(Guid TrabalhadorId, MetodoAutenticacaoAssinatura Metodo);

// Abstração do Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §5, etapa 3).
// Deliberadamente não cobre o futuro fluxo WebAuthn/FIDO2 (Fido2AutenticacaoStrategy, etapa 13) —
// aquele exige um desafio/resposta em duas chamadas (iniciar → registrar credencial assertion), uma
// forma incompatível com este método síncrono de um passo só. Cada estratégia concreta ganha sua
// própria interface quando chegar a vez dela, em vez de forçar um contrato genérico prematuro aqui.
public interface IAutenticacaoAssinaturaService
{
    Task<ResultadoAutenticacaoAssinatura> AutenticarPorCrachaOuQrAsync(string uid, string pin, CancellationToken ct);
}
