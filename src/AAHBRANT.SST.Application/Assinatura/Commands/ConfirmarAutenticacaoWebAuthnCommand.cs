using AAHBRANT.SST.Application.Assinatura.Queries;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// Passo 2 da assinatura biométrica — autentica via desafio/resposta WebAuthn e delega o registro
// (DocumentoSignatario + auditoria) a IRegistradorAssinaturaService, o mesmo usado por
// RegistrarAssinaturaCommand (crachá/QR + PIN) — ver comentário de IRegistradorAssinaturaService.
public record ConfirmarAutenticacaoWebAuthnCommand(Guid DocumentoAssinaturaId, string OpcoesJson, string RespostaJson, string? IpAddress = null) : IRequest<DocumentoSignatarioDto>;

public class ConfirmarAutenticacaoWebAuthnCommandValidator : AbstractValidator<ConfirmarAutenticacaoWebAuthnCommand>
{
    public ConfirmarAutenticacaoWebAuthnCommandValidator()
    {
        RuleFor(x => x.DocumentoAssinaturaId).NotEmpty();
        RuleFor(x => x.OpcoesJson).NotEmpty();
        RuleFor(x => x.RespostaJson).NotEmpty();
    }
}

public class ConfirmarAutenticacaoWebAuthnCommandHandler : IRequestHandler<ConfirmarAutenticacaoWebAuthnCommand, DocumentoSignatarioDto>
{
    private readonly IAutenticacaoWebAuthnService _webAuthn;
    private readonly IRegistradorAssinaturaService _registrador;

    public ConfirmarAutenticacaoWebAuthnCommandHandler(IAutenticacaoWebAuthnService webAuthn, IRegistradorAssinaturaService registrador)
    {
        _webAuthn = webAuthn;
        _registrador = registrador;
    }

    public async Task<DocumentoSignatarioDto> Handle(ConfirmarAutenticacaoWebAuthnCommand request, CancellationToken ct)
    {
        var resultado = await _webAuthn.ConfirmarAutenticacaoAsync(request.OpcoesJson, request.RespostaJson, ct);
        return await _registrador.RegistrarAsync(request.DocumentoAssinaturaId, resultado, request.IpAddress, ct);
    }
}
