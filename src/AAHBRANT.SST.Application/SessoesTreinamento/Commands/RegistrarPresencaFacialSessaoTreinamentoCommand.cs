using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Presença por reconhecimento facial na turma de treinamento. Mesma ideia da digital em fila:
// o serviço facial identifica quem está na foto dentro da obra, e aqui só conferimos se essa pessoa
// está inscrita na turma antes de confirmar a presença.
public record RegistrarPresencaFacialSessaoTreinamentoCommand(
    Guid SessaoTreinamentoId,
    Guid ObraId,
    byte[] FotoJpeg) : IRequest<Guid>;

public class RegistrarPresencaFacialSessaoTreinamentoCommandValidator : AbstractValidator<RegistrarPresencaFacialSessaoTreinamentoCommand>
{
    public RegistrarPresencaFacialSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.SessaoTreinamentoId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.FotoJpeg).NotEmpty();
    }
}

public class RegistrarPresencaFacialSessaoTreinamentoCommandHandler : IRequestHandler<RegistrarPresencaFacialSessaoTreinamentoCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService _autenticacaoFacial;

    public RegistrarPresencaFacialSessaoTreinamentoCommandHandler(IAppDbContext db, IAutenticacaoFacialService autenticacaoFacial)
    {
        _db = db;
        _autenticacaoFacial = autenticacaoFacial;
    }

    public async Task<Guid> Handle(RegistrarPresencaFacialSessaoTreinamentoCommand request, CancellationToken ct)
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

        var trabalhadorId = identificacao.Resultado!.TrabalhadorId;
        var participante = await _db.ParticipantesSessaoTreinamento
            .FirstOrDefaultAsync(p => p.SessaoTreinamentoId == request.SessaoTreinamentoId && p.TrabalhadorId == trabalhadorId, ct)
            ?? throw new KeyNotFoundException("Esta pessoa não está inscrita nesta turma.");

        if (participante.PresencaConfirmadaEm is not null)
            throw new InvalidOperationException("Presença já confirmada para este participante.");

        participante.PresencaConfirmadaEm = DateTime.UtcNow;
        participante.ScoreConfianca = identificacao.Confianca;

        await _db.SaveChangesAsync(ct);
        return trabalhadorId;
    }
}
