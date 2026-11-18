using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Quadro enviado pelo agente instalado na obra, sem usuário logado. Devolve nulo quando o agente não se
// autentica (segredo ausente/errado, agente inexistente, revogado ou de obra inativa): o controller responde 401.
// CapturadaEmUtc é o horário real do quadro (o agente o guarda quando fica sem internet).
public record AnalisarQuadroAgenteCameraCommand(
    Guid AgenteId, string? Segredo, string? VersaoAgente, Guid CameraId, byte[] Imagem, DateTime CapturadaEmUtc)
    : IRequest<ResultadoQuadroDto?>;

public class AnalisarQuadroAgenteCameraCommandValidator : AbstractValidator<AnalisarQuadroAgenteCameraCommand>
{
    public AnalisarQuadroAgenteCameraCommandValidator()
    {
        RuleFor(x => x.CameraId).NotEmpty();
        // Mesmas regras de AnalisarQuadroCameraCommand (4 MB, JPEG).
        RuleFor(x => x.Imagem)
            .NotEmpty()
            .Must(i => i.Length <= AnalisarQuadroCameraCommandValidator.TamanhoMaximoBytes).WithMessage("A imagem excede o tamanho máximo de 4 MB.")
            .Must(i => i.Length >= 2 && i[0] == 0xFF && i[1] == 0xD8).WithMessage("A imagem precisa estar em JPEG.");
    }
}

public class AnalisarQuadroAgenteCameraCommandHandler : IRequestHandler<AnalisarQuadroAgenteCameraCommand, ResultadoQuadroDto?>
{
    // O contato só é regravado a cada 30 s (ou quando a versão muda): vários quadros por segundo da mesma
    // obra disputariam a mesma linha do agente à toa. O mesmo vale para o registro do último quadro da câmera.
    public static readonly TimeSpan IntervaloGravacaoContato = ContatoAgenteCamera.IntervaloGravacao;

    private readonly IAppDbContext _db;
    private readonly IAgenteCameraAutenticador _autenticador;
    private readonly ICurrentUserService _usuarioAtual;
    private readonly ISender _mediator;
    private readonly ILogger<AnalisarQuadroAgenteCameraCommandHandler> _logger;

    public AnalisarQuadroAgenteCameraCommandHandler(
        IAppDbContext db,
        IAgenteCameraAutenticador autenticador,
        ICurrentUserService usuarioAtual,
        ISender mediator,
        ILogger<AnalisarQuadroAgenteCameraCommandHandler> logger)
    {
        _db = db;
        _autenticador = autenticador;
        _usuarioAtual = usuarioAtual;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ResultadoQuadroDto?> Handle(AnalisarQuadroAgenteCameraCommand request, CancellationToken ct)
    {
        var agente = await _autenticador.ValidarAsync(request.AgenteId, request.Segredo, ct);
        if (agente is null) return null;

        // Rota anônima com o Entra ligado: o EscopoPorObraMiddleware deixa o escopo ZERADO e os filtros globais
        // (Camera, OcorrenciaEpiCamera, Trabalhador, Obra, AreaSst...) escondem tudo. Só quebra em produção/hml,
        // não em dev. Com o segredo validado, o escopo desta requisição é a obra do agente, e só ela.
        _usuarioAtual.DefinirEscopo(temAcessoGlobal: false, new[] { agente.ObraId });

        await ContatoAgenteCamera.RegistrarAsync(_db, agente, request.VersaoAgente, _logger, ct);

        var camera = await _db.Cameras.FirstOrDefaultAsync(c => c.Id == request.CameraId && c.ObraId == agente.ObraId, ct)
            ?? throw new KeyNotFoundException($"Câmera {request.CameraId} não encontrada.");

        // Câmera IP cadastrada no servidor (tem equipamento): só o agente dono do equipamento pode enviar o quadro dela, e só se
        // ela for de fonte Rtsp. Câmera SEM equipamento é a do modo por arquivo local (etapas 1-2: o agente lê a lista do próprio
        // arquivo e a câmera no servidor é uma Camera comum, do navegador): segue valendo só a regra da obra, como sempre.
        if (camera.EquipamentoCameraId is { } equipamentoId)
        {
            var doAgente = camera.Fonte == FonteCamera.Rtsp && await _db.EquipamentosCamera.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(e => e.Id == equipamentoId && e.Ativo && e.ObraId == agente.ObraId && e.AgenteCameraId == agente.Id, ct);
            if (!doAgente) throw new KeyNotFoundException($"Câmera {request.CameraId} não encontrada.");
        }

        await RegistrarQuadroRecebidoAsync(camera, request.CapturadaEmUtc, ct);

        // Quadro do agente pode ter ficado guardado sem internet: o alerta sai com o horário real da captura.
        return await _mediator.Send(new AnalisarQuadroCameraCommand(request.CameraId, request.Imagem, request.CapturadaEmUtc, PermitirQuadroAtrasado: true), ct);
    }

    // Só anota que o quadro chegou (a situação "Recebendo imagem" sai daqui). Falha aqui nunca derruba a análise.
    // O horário do quadro é o da CAPTURA quando ele veio atrasado (fila offline do agente): "última foto" não pode mostrar a hora
    // em que a internet voltou. Quadro mais antigo que o último já registrado nunca recua esse horário.
    private async Task RegistrarQuadroRecebidoAsync(Camera camera, DateTime capturadaEmUtc, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var atrasado = agora - capturadaEmUtc > AnalisarQuadroCameraCommandHandler.ToleranciaQuadroAtrasado;
        var momento = atrasado ? capturadaEmUtc : agora;
        var recente = camera.UltimoQuadroRecebidoEmUtc is { } ultimo && momento - ultimo < IntervaloGravacaoContato;
        if (recente && camera.SituacaoConexao == SituacaoConexaoCamera.RecebendoImagem) return;

        camera.UltimoQuadroRecebidoEmUtc = camera.UltimoQuadroRecebidoEmUtc is { } anterior && anterior > momento ? anterior : momento;
        camera.SituacaoConexao = SituacaoConexaoCamera.RecebendoImagem;
        camera.MensagemConexao = null;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Último quadro da câmera {CameraId} não registrado.", camera.Id);
            _db.DescartarAlteracoesPendentes();
        }
    }
}
