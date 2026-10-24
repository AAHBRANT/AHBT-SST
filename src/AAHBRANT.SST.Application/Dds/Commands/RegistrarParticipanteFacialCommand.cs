using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Presença no DDS por reconhecimento facial — alternativa à digital (RegistrarParticipanteCommand),
// escolhida pelo operador ao clicar no botão facial da linha do funcionário. O Azure descobre quem
// está na foto dentro do PersonGroup da obra do DDS; aqui só se confere que é o funcionário da linha
// (senão a presença de A poderia ser gravada com o rosto de B) e se aplicam as mesmas regras da digital.
// A foto capturada fica guardada como evidência da presença (FotoTipo = Facial) e a mesma identificação
// vale como assinatura do DDS (melhor esforço), igual à digital.
public record RegistrarParticipanteFacialCommand(Guid DdsId, Guid TrabalhadorId, byte[] FotoJpeg) : IRequest<Guid>;

public class RegistrarParticipanteFacialCommandValidator : AbstractValidator<RegistrarParticipanteFacialCommand>
{
    public RegistrarParticipanteFacialCommandValidator()
    {
        RuleFor(x => x.DdsId).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.FotoJpeg).NotEmpty();
    }
}

public class RegistrarParticipanteFacialCommandHandler : IRequestHandler<RegistrarParticipanteFacialCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService _autenticacaoFacial;
    private readonly IMediator _mediator;
    private readonly IRegistradorAssinaturaService _registrador;
    private readonly ILogger<RegistrarParticipanteFacialCommandHandler> _logger;

    public RegistrarParticipanteFacialCommandHandler(
        IAppDbContext db,
        IAutenticacaoFacialService autenticacaoFacial,
        IMediator mediator,
        IRegistradorAssinaturaService registrador,
        ILogger<RegistrarParticipanteFacialCommandHandler> logger)
    {
        _db = db;
        _autenticacaoFacial = autenticacaoFacial;
        _mediator = mediator;
        _registrador = registrador;
        _logger = logger;
    }

    public async Task<Guid> Handle(RegistrarParticipanteFacialCommand request, CancellationToken ct)
    {
        var dds = await _db.Dds.Include(d => d.DdsSemanal).FirstOrDefaultAsync(d => d.Id == request.DdsId, ct)
            ?? throw new KeyNotFoundException($"DDS {request.DdsId} não encontrado.");
        if (dds.SemExpediente || dds.Status != StatusDds.EmAndamento || dds.DdsSemanal?.Status == StatusDdsSemanal.Concluida)
            throw new InvalidOperationException("Só é possível registrar presença em um DDS em andamento.");
        if (!await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId && t.ObraId == dds.ObraId && t.Ativo, ct))
            throw new InvalidOperationException("O funcionário deve pertencer à obra deste DDS.");
        if (await _db.DdsParticipantes.AnyAsync(p => p.DdsId == request.DdsId && p.TrabalhadorId == request.TrabalhadorId, ct))
            throw new InvalidOperationException("Este trabalhador já está registrado como participante deste DDS.");

        // ObraId vem do DDS, nunca do cliente: o rosto é procurado só no grupo da obra certa.
        var identificacao = await _autenticacaoFacial.IdentificarAsync(dds.ObraId, request.FotoJpeg, ct);
        if (!identificacao.Aceito)
            throw new RejeicaoFacialException(identificacao.Motivo!.Value, MensagemRejeicaoFacial.Para(identificacao.Motivo));

        var resultado = identificacao.Resultado!;
        if (resultado.TrabalhadorId != request.TrabalhadorId)
            throw new InvalidOperationException("O rosto reconhecido não corresponde ao funcionário selecionado. Tente novamente com o funcionário indicado.");

        var participante = new Domain.Entidades.DdsParticipante
        {
            DdsId = request.DdsId,
            TrabalhadorId = resultado.TrabalhadorId,
            FotoTipo = TipoFotoParticipante.Facial,
            FotoConteudo = request.FotoJpeg,
            FotoContentType = "image/jpeg",
            // Mesma escala 0-100 da digital (o Azure devolve 0-1).
            ScoreConfianca = identificacao.Confianca is { } c ? c * 100 : null,
        };
        _db.DdsParticipantes.Add(participante);
        var selecao = await _db.DdsFuncionariosSelecionados
            .Where(s => s.DdsId == dds.Id && s.TrabalhadorId == participante.TrabalhadorId).ToListAsync(ct);
        _db.DdsFuncionariosSelecionados.RemoveRange(selecao);
        await _db.SaveChangesAsync(ct);

        // Melhor esforço, como na digital: a presença já gravada é o que importa; se o Motor de
        // Assinatura falhar, o trabalhador ainda pode assinar depois por "Assinar DDS".
        try
        {
            var documentoId = await _mediator.Send(new CriarDocumentoAssinaturaCommand(nameof(Domain.Entidades.Dds), request.DdsId), ct);
            await _registrador.RegistrarAsync(documentoId, resultado, ipAddress: null, ct, request.FotoJpeg, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao registrar assinatura automática a partir da presença facial no DDS {DdsId} para o trabalhador {TrabalhadorId}.", request.DdsId, request.TrabalhadorId);
        }

        return participante.Id;
    }
}
