using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

public record AutenticarPorCrachaOuQrCommand(string Uid, string Pin) : IRequest<ResultadoAutenticacaoAssinatura>;

public class AutenticarPorCrachaOuQrCommandValidator : AbstractValidator<AutenticarPorCrachaOuQrCommand>
{
    public AutenticarPorCrachaOuQrCommandValidator()
    {
        RuleFor(x => x.Uid).NotEmpty();
        RuleFor(x => x.Pin).NotEmpty();
    }
}

public class AutenticarPorCrachaOuQrCommandHandler : IRequestHandler<AutenticarPorCrachaOuQrCommand, ResultadoAutenticacaoAssinatura>
{
    private readonly IAutenticacaoAssinaturaService _autenticacao;

    public AutenticarPorCrachaOuQrCommandHandler(IAutenticacaoAssinaturaService autenticacao) => _autenticacao = autenticacao;

    public Task<ResultadoAutenticacaoAssinatura> Handle(AutenticarPorCrachaOuQrCommand request, CancellationToken ct)
        => _autenticacao.AutenticarPorCrachaOuQrAsync(request.Uid, request.Pin, ct);
}
