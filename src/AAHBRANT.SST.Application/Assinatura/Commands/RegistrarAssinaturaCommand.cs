using AAHBRANT.SST.Application.Assinatura.Queries;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// Autentica (hoje: crachá/QR + PIN, via IAutenticacaoAssinaturaService — a troca para biometria FIDO2
// é só um novo registro de DI, sem mudar este comando), grava o DocumentoSignatario e registra o
// evento na TrilhaAuditoria (etapa 7). Ainda não gera o hash do documento em si (HashSha256 do PDF
// final): isso fica para a etapa 8-10 (finalização do documento), momento diferente do ciclo de vida
// (documento fechado, não mais aceitando assinaturas) — não confundir com o hash da cadeia de
// auditoria (HashRegistroAnterior/Atual), que é gerado por assinatura, não pelo documento inteiro.
public record RegistrarAssinaturaCommand(Guid DocumentoAssinaturaId, string Uid, string Pin, string? IpAddress = null) : IRequest<DocumentoSignatarioDto>;

public class RegistrarAssinaturaCommandValidator : AbstractValidator<RegistrarAssinaturaCommand>
{
    public RegistrarAssinaturaCommandValidator()
    {
        RuleFor(x => x.DocumentoAssinaturaId).NotEmpty();
        RuleFor(x => x.Uid).NotEmpty();
        RuleFor(x => x.Pin).NotEmpty();
    }
}

public class RegistrarAssinaturaCommandHandler : IRequestHandler<RegistrarAssinaturaCommand, DocumentoSignatarioDto>
{
    private readonly IAutenticacaoAssinaturaService _autenticacao;
    private readonly IRegistradorAssinaturaService _registrador;

    public RegistrarAssinaturaCommandHandler(IAutenticacaoAssinaturaService autenticacao, IRegistradorAssinaturaService registrador)
    {
        _autenticacao = autenticacao;
        _registrador = registrador;
    }

    public async Task<DocumentoSignatarioDto> Handle(RegistrarAssinaturaCommand request, CancellationToken ct)
    {
        var resultado = await _autenticacao.AutenticarPorCrachaOuQrAsync(request.Uid, request.Pin, ct);
        return await _registrador.RegistrarAsync(request.DocumentoAssinaturaId, resultado, request.IpAddress, ct);
    }
}
