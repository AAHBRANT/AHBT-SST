using AAHBRANT.SST.Application.Assinatura.Queries;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

public record RegistrarAssinaturaBiometriaLocalCommand(
    Guid DocumentoAssinaturaId, Guid DispositivoId, string SegredoDispositivo, Guid TrabalhadorId, double Score, string? IpAddress = null) : IRequest<DocumentoSignatarioDto>;

public class RegistrarAssinaturaBiometriaLocalCommandValidator : AbstractValidator<RegistrarAssinaturaBiometriaLocalCommand>
{
    public RegistrarAssinaturaBiometriaLocalCommandValidator()
    {
        RuleFor(x => x.DocumentoAssinaturaId).NotEmpty();
        RuleFor(x => x.DispositivoId).NotEmpty();
        RuleFor(x => x.SegredoDispositivo).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Score).InclusiveBetween(0, 100);
    }
}

public class RegistrarAssinaturaBiometriaLocalCommandHandler : IRequestHandler<RegistrarAssinaturaBiometriaLocalCommand, DocumentoSignatarioDto>
{
    private readonly IAutenticacaoBiometriaLocalService _autenticacao;
    private readonly IRegistradorAssinaturaService _registrador;

    public RegistrarAssinaturaBiometriaLocalCommandHandler(IAutenticacaoBiometriaLocalService autenticacao, IRegistradorAssinaturaService registrador)
    {
        _autenticacao = autenticacao;
        _registrador = registrador;
    }

    public async Task<DocumentoSignatarioDto> Handle(RegistrarAssinaturaBiometriaLocalCommand request, CancellationToken ct)
    {
        var resultado = await _autenticacao.AutenticarPorMatchLocalAsync(
            request.DispositivoId, request.SegredoDispositivo, request.TrabalhadorId, request.Score, ct);
        return await _registrador.RegistrarAsync(request.DocumentoAssinaturaId, resultado, request.IpAddress, ct);
    }
}
