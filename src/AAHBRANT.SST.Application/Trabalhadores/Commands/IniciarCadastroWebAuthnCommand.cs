using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

// Passo 1 do cadastro de credencial biométrica (etapa 13 do Motor de Assinatura Eletrônica) — devolve
// o desafio (CredentialCreateOptions em JSON) que o navegador repassa a navigator.credentials.create().
// IAutenticacaoWebAuthnService.IniciarCadastroAsync já valida Termo de Aceite/Consentimento Biometria.
public record IniciarCadastroWebAuthnCommand(Guid TrabalhadorId, TipoAutenticadorWebAuthn Tipo) : IRequest<string>;

public class IniciarCadastroWebAuthnCommandValidator : AbstractValidator<IniciarCadastroWebAuthnCommand>
{
    public IniciarCadastroWebAuthnCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Tipo).IsInEnum();
    }
}

public class IniciarCadastroWebAuthnCommandHandler : IRequestHandler<IniciarCadastroWebAuthnCommand, string>
{
    private readonly IAutenticacaoWebAuthnService _webAuthn;

    public IniciarCadastroWebAuthnCommandHandler(IAutenticacaoWebAuthnService webAuthn) => _webAuthn = webAuthn;

    public Task<string> Handle(IniciarCadastroWebAuthnCommand request, CancellationToken ct)
        => _webAuthn.IniciarCadastroAsync(request.TrabalhadorId, request.Tipo, ct);
}
