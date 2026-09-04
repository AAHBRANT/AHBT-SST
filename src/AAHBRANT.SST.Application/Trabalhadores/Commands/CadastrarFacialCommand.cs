using AAHBRANT.SST.Application.Assinatura;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

public record CadastrarFacialCommand(Guid TrabalhadorId, byte[] FotoJpeg) : IRequest;

public class CadastrarFacialCommandValidator : AbstractValidator<CadastrarFacialCommand>
{
    public CadastrarFacialCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.FotoJpeg).NotEmpty();
    }
}

public class CadastrarFacialCommandHandler : IRequestHandler<CadastrarFacialCommand>
{
    private readonly IAutenticacaoFacialService _autenticacaoFacial;

    public CadastrarFacialCommandHandler(IAutenticacaoFacialService autenticacaoFacial) => _autenticacaoFacial = autenticacaoFacial;

    public async Task Handle(CadastrarFacialCommand request, CancellationToken ct)
    {
        await _autenticacaoFacial.CadastrarAsync(request.TrabalhadorId, request.FotoJpeg, ct);
    }
}
