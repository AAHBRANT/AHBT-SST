using AAHBRANT.SST.Application.Assinatura.Queries;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// Diferente de RegistrarAssinaturaBiometriaLocalCommand: não recebe TrabalhadorId — quem está na
// foto é descoberto pelo Azure (Identify), não resolvido antes pelo cliente.
public record RegistrarAssinaturaFacialCommand(Guid DocumentoAssinaturaId, Guid ObraId, byte[] FotoJpeg, string? IpAddress = null) : IRequest<DocumentoSignatarioDto>;

public class RegistrarAssinaturaFacialCommandValidator : AbstractValidator<RegistrarAssinaturaFacialCommand>
{
    public RegistrarAssinaturaFacialCommandValidator()
    {
        RuleFor(x => x.DocumentoAssinaturaId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.FotoJpeg).NotEmpty();
    }
}

// Mensagem de erro de negócio distinta por motivo (spec §3) — o controller devolve isso como corpo
// do 400 para a UI mostrar o texto certo (nenhum rosto / múltiplos rostos / confiança baixa / não
// reconhecido), em vez de um genérico "falha na autenticação".
public class RejeicaoFacialException : Exception
{
    public MotivoRejeicaoFacial Motivo { get; }
    public RejeicaoFacialException(MotivoRejeicaoFacial motivo, string mensagem) : base(mensagem) => Motivo = motivo;
}

public class RegistrarAssinaturaFacialCommandHandler : IRequestHandler<RegistrarAssinaturaFacialCommand, DocumentoSignatarioDto>
{
    private readonly IAutenticacaoFacialService _autenticacaoFacial;
    private readonly IRegistradorAssinaturaService _registrador;

    public RegistrarAssinaturaFacialCommandHandler(IAutenticacaoFacialService autenticacaoFacial, IRegistradorAssinaturaService registrador)
    {
        _autenticacaoFacial = autenticacaoFacial;
        _registrador = registrador;
    }

    public async Task<DocumentoSignatarioDto> Handle(RegistrarAssinaturaFacialCommand request, CancellationToken ct)
    {
        var identificacao = await _autenticacaoFacial.IdentificarAsync(request.ObraId, request.FotoJpeg, ct);
        if (!identificacao.Aceito)
        {
            var mensagem = identificacao.Motivo switch
            {
                MotivoRejeicaoFacial.NenhumRostoDetectado => "Nenhum rosto detectado na foto.",
                MotivoRejeicaoFacial.MultiplosRostosDetectados => "Mais de uma pessoa detectada na câmera — aproxime-se sozinho.",
                MotivoRejeicaoFacial.ConfiancaBaixa => "Rosto reconhecido com baixa confiança — tente novamente com melhor iluminação.",
                _ => "Rosto não reconhecido.",
            };
            throw new RejeicaoFacialException(identificacao.Motivo!.Value, mensagem);
        }

        return await _registrador.RegistrarAsync(request.DocumentoAssinaturaId, identificacao.Resultado!, request.IpAddress, ct);
    }
}
