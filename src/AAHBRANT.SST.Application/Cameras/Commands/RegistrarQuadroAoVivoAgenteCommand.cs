using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Quadro JPEG da câmera ao vivo enviado pelo agente (rota anônima, autenticada pelo segredo). Grava SÓ o último quadro na
// linha da sessão (sobrescreve, sequência crescente). A sessão só aceita quadro do agente dono da câmera: sessão de outro
// agente é "não encontrada". Sessão inexistente = KeyNotFoundException (404 sessao_nao_encontrada).
public record RegistrarQuadroAoVivoAgenteCommand(
    Guid AgenteId,
    string? Segredo,
    string? VersaoAgente,
    Guid SessaoId,
    byte[] Jpeg,
    DateTime? CapturadoEmUtc) : IRequest<ResultadoQuadroAoVivo>
{
    // O corpo é uma imagem de pessoa possível: nunca vai para log, nem por ToString.
    public override string ToString() => $"{nameof(RegistrarQuadroAoVivoAgenteCommand)} {{ SessaoId = {SessaoId}, Bytes = {Jpeg.Length} }}";
}

public enum ResultadoQuadroAoVivo
{
    NaoAutenticado,
    // Gravado: o agente continua.
    Continuar,
    // Sessão encerrada (410): o agente para já.
    SessaoEncerrada,
    // Acima do teto de quadros da sessão (429): o agente espera e segue.
    LimiteExcedido,
}

public class RegistrarQuadroAoVivoAgenteCommandValidator : AbstractValidator<RegistrarQuadroAoVivoAgenteCommand>
{
    public RegistrarQuadroAoVivoAgenteCommandValidator()
    {
        RuleFor(x => x.SessaoId).NotEmpty();
        RuleFor(x => x.Jpeg).Must(j => j is { Length: >= 4 } && j[0] == 0xFF && j[1] == 0xD8)
            .WithMessage("O quadro precisa estar em JPEG.");
    }
}

public class RegistrarQuadroAoVivoAgenteCommandHandler : IRequestHandler<RegistrarQuadroAoVivoAgenteCommand, ResultadoQuadroAoVivo>
{
    public const string CodigoSessaoNaoEncontrada = "sessao_nao_encontrada";

    // Horário de captura além disto no futuro é relógio errado do PC da obra: vale o do servidor.
    private static readonly TimeSpan ToleranciaRelogio = TimeSpan.FromMinutes(5);

    private readonly IAppDbContext _db;
    private readonly IAgenteCameraAutenticador _autenticador;
    private readonly ICurrentUserService _usuarioAtual;
    private readonly ILogger<RegistrarQuadroAoVivoAgenteCommandHandler> _logger;
    private readonly AoVivoCameraOpcoes _opcoes;

    public RegistrarQuadroAoVivoAgenteCommandHandler(
        IAppDbContext db,
        IAgenteCameraAutenticador autenticador,
        ICurrentUserService usuarioAtual,
        ILogger<RegistrarQuadroAoVivoAgenteCommandHandler> logger,
        IOptions<AoVivoCameraOpcoes>? opcoes = null)
    {
        _db = db;
        _autenticador = autenticador;
        _usuarioAtual = usuarioAtual;
        _logger = logger;
        _opcoes = opcoes?.Value ?? new AoVivoCameraOpcoes();
    }

    public async Task<ResultadoQuadroAoVivo> Handle(RegistrarQuadroAoVivoAgenteCommand request, CancellationToken ct)
    {
        var agente = await _autenticador.ValidarAsync(request.AgenteId, request.Segredo, ct);
        if (agente is null) return ResultadoQuadroAoVivo.NaoAutenticado;

        _usuarioAtual.DefinirEscopo(temAcessoGlobal: false, new[] { agente.ObraId });
        await ContatoAgenteCamera.RegistrarAsync(_db, agente, request.VersaoAgente, _logger, ct);

        var agora = DateTime.UtcNow;

        // Sem filtro global (rota anônima) e sem JOIN: só sessão deste agente e desta obra, sem o quadro anterior.
        var sessao = await _db.SessoesAoVivoCamera.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.Id == request.SessaoId && s.Ativo && s.ObraId == agente.ObraId && s.AgenteCameraId == agente.Id)
            .Select(s => new
            {
                s.Id, s.CameraId, s.EncerradaEmUtc, s.LimiteEmUtc, s.UltimaVisualizacaoEmUtc, s.SolicitadaEmUtc, s.IniciadaEmUtc,
                s.Fps, s.QuadrosRecebidos, s.UltimoQuadroRecebidoEmUtc,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException(CodigoSessaoNaoEncontrada);

        if (sessao.EncerradaEmUtc is not null) return ResultadoQuadroAoVivo.SessaoEncerrada;

        IQueryable<Domain.Entidades.SessaoAoVivoCamera> Esta() => _db.SessoesAoVivoCamera.IgnoreQueryFilters().Where(s => s.Id == sessao.Id);

        var parada = SessaoAoVivoCameraRegras.MotivoDeParada(
            sessao.LimiteEmUtc, sessao.UltimaVisualizacaoEmUtc, sessao.SolicitadaEmUtc, sessao.IniciadaEmUtc,
            sessao.UltimoQuadroRecebidoEmUtc, agora, _opcoes);
        if (parada is not null)
        {
            await SessaoAoVivoCameraRegras.EncerrarAsync(Esta(), parada, agora, ct);
            return ResultadoQuadroAoVivo.SessaoEncerrada;
        }

        var cameraOk = await _db.Cameras.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(c => c.Id == sessao.CameraId && c.Ativo && c.Habilitada, ct);
        if (!cameraOk)
        {
            await SessaoAoVivoCameraRegras.EncerrarAsync(Esta(), SessaoAoVivoCameraRegras.MotivoCameraDesabilitada, agora, ct);
            return ResultadoQuadroAoVivo.SessaoEncerrada;
        }

        if (SessaoAoVivoCameraRegras.ExcedeTetoDeQuadros(sessao.QuadrosRecebidos, sessao.Fps, sessao.IniciadaEmUtc, agora))
            return ResultadoQuadroAoVivo.LimiteExcedido;

        var capturadoEm = request.CapturadoEmUtc is { } c && c <= agora + ToleranciaRelogio ? c : agora;
        var jpeg = request.Jpeg;
        var tamanho = (long)jpeg.Length;

        // Uma única gravação atômica no SQL, só se a sessão ainda está aberta (a tela pode tê-la fechado agora há pouco).
        var gravadas = await Esta().Where(s => s.EncerradaEmUtc == null).ExecuteUpdateAsync(set => set
            .SetProperty(s => s.UltimoQuadro, jpeg)
            .SetProperty(s => s.UltimoQuadroSequencia, s => s.UltimoQuadroSequencia + 1)
            .SetProperty(s => s.UltimoQuadroCapturadoEmUtc, (DateTime?)capturadoEm)
            .SetProperty(s => s.UltimoQuadroRecebidoEmUtc, (DateTime?)agora)
            .SetProperty(s => s.QuadrosRecebidos, s => s.QuadrosRecebidos + 1)
            .SetProperty(s => s.BytesRecebidos, s => s.BytesRecebidos + tamanho)
            .SetProperty(s => s.IniciadaEmUtc, s => s.IniciadaEmUtc ?? agora), ct);

        return gravadas > 0 ? ResultadoQuadroAoVivo.Continuar : ResultadoQuadroAoVivo.SessaoEncerrada;
    }
}
