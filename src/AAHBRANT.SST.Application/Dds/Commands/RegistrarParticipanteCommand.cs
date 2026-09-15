using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Evidência de presença passou a ser exclusivamente biométrica (2026-08-31, pedido do usuário) —
// reaproveita IAutenticacaoBiometriaLocalService (mesmo serviço do Motor de Assinatura Eletrônica):
// o match 1:N já aconteceu no agente local (leitor Futronic FS80H), aqui só se reautentica o
// dispositivo e se confere o score contra o limiar configurado antes de gravar a presença.
//
// A mesma digital vale como assinatura do DDS (04/09, pedido do usuário: "não dá certo ficar em
// aberto, cada usuário assina só uma vez") — depois de gravar a presença, registra também (melhor
// esforço, nunca bloqueia a presença em si) uma assinatura no Motor de Assinatura Eletrônica para
// este trabalhador neste DDS, reaproveitando o MESMO resultado já autenticado acima — sem pedir uma
// segunda leitura. Quem quiser ver/gerar o comprovante formal continua indo em "Assinar DDS", que
// já mostra esse trabalhador como assinado.
public record RegistrarParticipanteCommand(
    Guid DdsId,
    Guid TrabalhadorId,
    Guid DispositivoId,
    string SegredoDispositivo,
    double Score) : IRequest<Guid>;

public class RegistrarParticipanteCommandValidator : AbstractValidator<RegistrarParticipanteCommand>
{
    public RegistrarParticipanteCommandValidator()
    {
        RuleFor(x => x.DdsId).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.DispositivoId).NotEmpty();
        RuleFor(x => x.SegredoDispositivo).NotEmpty();
        RuleFor(x => x.Score).InclusiveBetween(0, 100);
    }
}

public class RegistrarParticipanteCommandHandler : IRequestHandler<RegistrarParticipanteCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoBiometriaLocalService _autenticacao;
    private readonly IMediator _mediator;
    private readonly IRegistradorAssinaturaService _registrador;
    private readonly ILogger<RegistrarParticipanteCommandHandler> _logger;

    public RegistrarParticipanteCommandHandler(
        IAppDbContext db,
        IAutenticacaoBiometriaLocalService autenticacao,
        IMediator mediator,
        IRegistradorAssinaturaService registrador,
        ILogger<RegistrarParticipanteCommandHandler> logger)
    {
        _db = db;
        _autenticacao = autenticacao;
        _mediator = mediator;
        _registrador = registrador;
        _logger = logger;
    }

    public async Task<Guid> Handle(RegistrarParticipanteCommand request, CancellationToken ct)
    {
        var ddsExiste = await _db.Dds.AnyAsync(d => d.Id == request.DdsId, ct);
        if (!ddsExiste)
            throw new KeyNotFoundException($"DDS {request.DdsId} não encontrado.");

        var jaParticipa = await _db.DdsParticipantes
            .AnyAsync(p => p.DdsId == request.DdsId && p.TrabalhadorId == request.TrabalhadorId, ct);
        if (jaParticipa)
            throw new InvalidOperationException("Este trabalhador já está registrado como participante deste DDS.");

        var resultado = await _autenticacao.AutenticarPorMatchLocalAsync(
            request.DispositivoId, request.SegredoDispositivo, request.TrabalhadorId, request.Score, ct);

        var participante = new Domain.Entidades.DdsParticipante
        {
            DdsId = request.DdsId,
            TrabalhadorId = resultado.TrabalhadorId,
            FotoTipo = TipoFotoParticipante.Biometria,
            ScoreConfianca = request.Score,
        };
        _db.DdsParticipantes.Add(participante);
        await _db.SaveChangesAsync(ct);

        // Melhor esforço: a presença já registrada acima é o que importa de verdade — se o Motor de
        // Assinatura falhar por qualquer motivo (documento cancelado, trabalhador já assinou por
        // outra via etc.), a presença continua válida e o trabalhador pode assinar depois pela tela
        // "Assinar DDS" normalmente.
        try
        {
            var documentoId = await _mediator.Send(new CriarDocumentoAssinaturaCommand(nameof(Domain.Entidades.Dds), request.DdsId), ct);
            await _registrador.RegistrarAsync(documentoId, resultado, ipAddress: null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao registrar assinatura automática a partir da presença no DDS {DdsId} para o trabalhador {TrabalhadorId}.", request.DdsId, resultado.TrabalhadorId);
        }

        return participante.Id;
    }
}
