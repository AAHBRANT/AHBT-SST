using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Tela: abre (ou reaproveita) a sessão "quase ao vivo" de uma câmera IP. Vários espectadores da mesma câmera dividem uma
// sessão e uma transmissão. Câmera inexistente ou fora do escopo do usuário = KeyNotFoundException (404). Recusas
// previsíveis voltam em CodigoRecusa (a tela mostra a mensagem pelo código): 409 para as condições da câmera/agente e
// 429 para limite_sessoes (já há câmeras ao vivo demais neste agente).
public record IniciarSessaoAoVivoCameraCommand(Guid CameraId) : IRequest<IniciarSessaoAoVivoResultado>;

public record SessaoAoVivoIniciadaDto(Guid SessaoId, string Situacao, int Fps, int EncerraEmSegundos);

public record IniciarSessaoAoVivoResultado(SessaoAoVivoIniciadaDto? Sessao, string? CodigoRecusa)
{
    public bool LimiteDeSessoes => CodigoRecusa == SessaoAoVivoCameraRegras.CodigoLimiteSessoes;
}

public class IniciarSessaoAoVivoCameraCommandHandler : IRequestHandler<IniciarSessaoAoVivoCameraCommand, IniciarSessaoAoVivoResultado>
{
    public const string Aguardando = "Aguardando";
    public const string AoVivo = "AoVivo";

    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _usuarioAtual;
    private readonly AoVivoCameraOpcoes _opcoes;

    public IniciarSessaoAoVivoCameraCommandHandler(IAppDbContext db, ICurrentUserService usuarioAtual, IOptions<AoVivoCameraOpcoes>? opcoes = null)
    {
        _db = db;
        _usuarioAtual = usuarioAtual;
        _opcoes = opcoes?.Value ?? new AoVivoCameraOpcoes();
    }

    public async Task<IniciarSessaoAoVivoResultado> Handle(IniciarSessaoAoVivoCameraCommand request, CancellationToken ct)
    {
        // O filtro global já restringe câmera, equipamento e agente à obra do usuário: o que não é dele é "não encontrado".
        var camera = await _db.Cameras.AsNoTracking().Where(c => c.Id == request.CameraId)
            .Select(c => new { c.Id, c.ObraId, c.Fonte, c.Habilitada, c.EquipamentoCameraId })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Câmera {request.CameraId} não encontrada.");

        if (camera.Fonte != FonteCamera.Rtsp || camera.EquipamentoCameraId is not { } equipamentoId)
            return Recusa(SessaoAoVivoCameraRegras.CodigoNaoECameraIp);
        if (!camera.Habilitada)
            return Recusa(SessaoAoVivoCameraRegras.CodigoCameraDesabilitada);

        // Equipamento excluído: a câmera já não vai na configuração do agente, então não há de onde transmitir.
        var agenteId = await _db.EquipamentosCamera.AsNoTracking().Where(e => e.Id == equipamentoId)
            .Select(e => (Guid?)e.AgenteCameraId).FirstOrDefaultAsync(ct);
        if (agenteId is null) return Recusa(SessaoAoVivoCameraRegras.CodigoNaoECameraIp);

        var agente = await _db.AgentesCamera.AsNoTracking().Where(a => a.Id == agenteId)
            .Select(a => new { a.ObraId, a.Ativo, a.Versao, a.UltimoContatoEmUtc }).FirstOrDefaultAsync(ct);
        if (agente is null || !agente.Ativo) return Recusa(SessaoAoVivoCameraRegras.CodigoAgenteRevogado);
        if (agente.ObraId != camera.ObraId) throw new InvalidOperationException("O agente do equipamento é de outra obra.");

        var agora = DateTime.UtcNow;
        if (agente.UltimoContatoEmUtc is not { } contato || agora - contato > SituacaoConexaoCameraRegras.LimiteSemContatoDoAgente)
            return Recusa(SessaoAoVivoCameraRegras.CodigoAgenteSemContato);
        if (!SessaoAoVivoCameraRegras.AgenteSuportaAoVivo(agente.Versao))
            return Recusa(SessaoAoVivoCameraRegras.CodigoAgenteDesatualizado);

        await SessaoAoVivoCameraRegras.EncerrarParadasAsync(_db, _opcoes, agenteId, agora, ct);

        var existente = await ObterAbertaAsync(camera.Id, ct);
        if (existente is not null) return await ReaproveitarAsync(existente.Id, existente, agora, ct);

        var abertasNoAgente = await _db.SessoesAoVivoCamera.AsNoTracking()
            .CountAsync(s => s.AgenteCameraId == agenteId && s.EncerradaEmUtc == null, ct);
        if (abertasNoAgente >= _opcoes.MaximoCamerasPorAgente) return Recusa(SessaoAoVivoCameraRegras.CodigoLimiteSessoes);

        var sessao = new SessaoAoVivoCamera
        {
            ObraId = camera.ObraId,
            CameraId = camera.Id,
            AgenteCameraId = agenteId.Value,
            SolicitadaPorUsuarioId = _usuarioAtual.UsuarioId,
            SolicitadaEmUtc = agora,
            UltimaVisualizacaoEmUtc = agora,
            LimiteEmUtc = agora.AddSeconds(_opcoes.DuracaoMaximaSegundos),
            Fps = _opcoes.FpsEfetivo,
        };
        _db.SessoesAoVivoCamera.Add(sessao);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (!ct.IsCancellationRequested)
        {
            // Outra tela (ou outra réplica) abriu esta mesma câmera no mesmo instante: o índice único de sessão aberta
            // barrou a segunda. Reaproveita a que ganhou.
            _db.DescartarAlteracoesPendentes();
            var vencedora = await ObterAbertaAsync(camera.Id, ct);
            if (vencedora is null) throw;
            return await ReaproveitarAsync(vencedora.Id, vencedora, agora, ct);
        }

        return Aceita(sessao.Id, aberta: false, sessao.Fps, sessao.LimiteEmUtc, agora);
    }

    private sealed record Aberta(Guid Id, DateTime? IniciadaEmUtc, DateTime LimiteEmUtc, int Fps);

    private Task<Aberta?> ObterAbertaAsync(Guid cameraId, CancellationToken ct)
        => _db.SessoesAoVivoCamera.AsNoTracking().Where(s => s.CameraId == cameraId && s.EncerradaEmUtc == null)
            .Select(s => new Aberta(s.Id, s.IniciadaEmUtc, s.LimiteEmUtc, s.Fps)).FirstOrDefaultAsync(ct);

    // Mais um espectador: conta como visualização (segura a sessão contra a inatividade).
    private async Task<IniciarSessaoAoVivoResultado> ReaproveitarAsync(Guid id, Aberta s, DateTime agora, CancellationToken ct)
    {
        await _db.SessoesAoVivoCamera.IgnoreQueryFilters()
            .Where(x => x.Id == id && x.EncerradaEmUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UltimaVisualizacaoEmUtc, agora), ct);
        return Aceita(s.Id, s.IniciadaEmUtc is not null, s.Fps, s.LimiteEmUtc, agora);
    }

    private static IniciarSessaoAoVivoResultado Aceita(Guid id, bool aberta, int fps, DateTime limite, DateTime agora)
        => new(new SessaoAoVivoIniciadaDto(id, aberta ? AoVivo : Aguardando, fps, SessaoAoVivoCameraRegras.SegundosRestantes(limite, agora)), null);

    private static IniciarSessaoAoVivoResultado Recusa(string codigo) => new(null, codigo);
}
