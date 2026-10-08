using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Fila facial do DDS: a câmera fica aberta e cada funcionário só olha para ela. Diferente de
// RegistrarParticipanteFacialCommand (o operador escolhe o funcionário da linha), aqui o Azure
// descobre quem é (1:N) dentro do grupo da obra do DDS. Como ninguém confere a pessoa na tela, a
// identificação exige margem sobre o segundo colocado: dois candidatos próximos são recusados e o
// funcionário usa a digital. Mesmas regras da digital: DDS em andamento, funcionário da obra, e a
// presença já confirmada não é gravada de novo (devolve JaConfirmado para a tela avisar).
public record RegistrarParticipanteFacialFilaCommand(Guid DdsId, byte[] FotoJpeg) : IRequest<ResultadoPresencaFacialFila>;

public record ResultadoPresencaFacialFila(Guid TrabalhadorId, string TrabalhadorNome, bool JaConfirmado, double? Confianca);

public class RegistrarParticipanteFacialFilaCommandValidator : AbstractValidator<RegistrarParticipanteFacialFilaCommand>
{
    public RegistrarParticipanteFacialFilaCommandValidator()
    {
        RuleFor(x => x.DdsId).NotEmpty();
        RuleFor(x => x.FotoJpeg).NotEmpty();
    }
}

public class RegistrarParticipanteFacialFilaCommandHandler : IRequestHandler<RegistrarParticipanteFacialFilaCommand, ResultadoPresencaFacialFila>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService _autenticacaoFacial;
    private readonly IMediator _mediator;
    private readonly IRegistradorAssinaturaService _registrador;
    private readonly ILogger<RegistrarParticipanteFacialFilaCommandHandler> _logger;

    public RegistrarParticipanteFacialFilaCommandHandler(
        IAppDbContext db,
        IAutenticacaoFacialService autenticacaoFacial,
        IMediator mediator,
        IRegistradorAssinaturaService registrador,
        ILogger<RegistrarParticipanteFacialFilaCommandHandler> logger)
    {
        _db = db;
        _autenticacaoFacial = autenticacaoFacial;
        _mediator = mediator;
        _registrador = registrador;
        _logger = logger;
    }

    public async Task<ResultadoPresencaFacialFila> Handle(RegistrarParticipanteFacialFilaCommand request, CancellationToken ct)
    {
        var dds = await _db.Dds.Include(d => d.DdsSemanal).FirstOrDefaultAsync(d => d.Id == request.DdsId, ct)
            ?? throw new KeyNotFoundException($"DDS {request.DdsId} não encontrado.");
        if (dds.SemExpediente || dds.Status != StatusDds.EmAndamento || dds.DdsSemanal?.Status == StatusDdsSemanal.Concluida)
            throw new InvalidOperationException("Só é possível registrar presença em um DDS em andamento.");

        // ObraId vem do DDS, nunca do cliente: o rosto é procurado só no grupo da obra certa.
        var identificacao = await _autenticacaoFacial.IdentificarAsync(dds.ObraId, request.FotoJpeg, ct, exigirMargemSobreSegundoColocado: true);
        if (!identificacao.Aceito)
            throw new RejeicaoFacialException(identificacao.Motivo!.Value, MensagemRejeicaoFacial.Para(identificacao.Motivo));

        var resultado = identificacao.Resultado!;
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == resultado.TrabalhadorId && t.ObraId == dds.ObraId && t.Ativo, ct)
            ?? throw new InvalidOperationException("O funcionário reconhecido não pertence à obra deste DDS.");

        if (await _db.DdsParticipantes.AnyAsync(p => p.DdsId == request.DdsId && p.TrabalhadorId == trabalhador.Id, ct))
            return new ResultadoPresencaFacialFila(trabalhador.Id, trabalhador.Nome, JaConfirmado: true, identificacao.Confianca);

        var participante = new Domain.Entidades.DdsParticipante
        {
            DdsId = request.DdsId,
            TrabalhadorId = trabalhador.Id,
            FotoTipo = TipoFotoParticipante.Facial,
            FotoConteudo = request.FotoJpeg,
            FotoContentType = "image/jpeg",
            // Mesma escala 0-100 da digital (o Azure devolve 0-1).
            ScoreConfianca = identificacao.Confianca is { } c ? c * 100 : null,
        };
        _db.DdsParticipantes.Add(participante);
        var selecao = await _db.DdsFuncionariosSelecionados
            .Where(s => s.DdsId == dds.Id && s.TrabalhadorId == trabalhador.Id).ToListAsync(ct);
        _db.DdsFuncionariosSelecionados.RemoveRange(selecao);
        await _db.SaveChangesAsync(ct);

        // Melhor esforço, como na digital e no facial por linha: a presença gravada é o que importa.
        try
        {
            var documentoId = await _mediator.Send(new CriarDocumentoAssinaturaCommand(nameof(Domain.Entidades.Dds), request.DdsId), ct);
            await _registrador.RegistrarAsync(documentoId, resultado, ipAddress: null, ct, request.FotoJpeg, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao registrar assinatura automática a partir da fila facial no DDS {DdsId} para o trabalhador {TrabalhadorId}.", request.DdsId, trabalhador.Id);
        }

        return new ResultadoPresencaFacialFila(trabalhador.Id, trabalhador.Nome, JaConfirmado: false, identificacao.Confianca);
    }
}
