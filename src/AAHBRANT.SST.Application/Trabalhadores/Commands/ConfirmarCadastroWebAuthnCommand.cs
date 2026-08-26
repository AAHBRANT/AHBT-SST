using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

// Passo 2 do cadastro de credencial biométrica — recebe a resposta que o autenticador (leitor da obra
// ou celular) devolveu para o desafio gerado por IniciarCadastroWebAuthnCommand e persiste a
// CredencialWebAuthn.
public record ConfirmarCadastroWebAuthnCommand(Guid TrabalhadorId, TipoAutenticadorWebAuthn Tipo, string OpcoesJson, string RespostaJson) : IRequest;

public class ConfirmarCadastroWebAuthnCommandValidator : AbstractValidator<ConfirmarCadastroWebAuthnCommand>
{
    public ConfirmarCadastroWebAuthnCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.OpcoesJson).NotEmpty();
        RuleFor(x => x.RespostaJson).NotEmpty();
    }
}

public class ConfirmarCadastroWebAuthnCommandHandler : IRequestHandler<ConfirmarCadastroWebAuthnCommand>
{
    private readonly IAutenticacaoWebAuthnService _webAuthn;

    public ConfirmarCadastroWebAuthnCommandHandler(IAutenticacaoWebAuthnService webAuthn) => _webAuthn = webAuthn;

    public Task Handle(ConfirmarCadastroWebAuthnCommand request, CancellationToken ct)
        => _webAuthn.ConfirmarCadastroAsync(request.TrabalhadorId, request.Tipo, request.OpcoesJson, request.RespostaJson, ct);
}
