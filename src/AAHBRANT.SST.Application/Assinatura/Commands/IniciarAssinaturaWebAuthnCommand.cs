using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// Passo 1 da assinatura biométrica no quiosque (etapa 13) — TrabalhadorId nulo = leitor compartilhado
// da obra (a credencial "discoverable" resolve a identidade só na resposta); preenchido = celular
// próprio, que já sabe de quem é o aparelho.
public record IniciarAssinaturaWebAuthnCommand(Guid? TrabalhadorId) : IRequest<string>;

public class IniciarAssinaturaWebAuthnCommandHandler : IRequestHandler<IniciarAssinaturaWebAuthnCommand, string>
{
    private readonly IAutenticacaoWebAuthnService _webAuthn;

    public IniciarAssinaturaWebAuthnCommandHandler(IAutenticacaoWebAuthnService webAuthn) => _webAuthn = webAuthn;

    public Task<string> Handle(IniciarAssinaturaWebAuthnCommand request, CancellationToken ct)
        => _webAuthn.IniciarAutenticacaoAsync(request.TrabalhadorId, ct);
}
