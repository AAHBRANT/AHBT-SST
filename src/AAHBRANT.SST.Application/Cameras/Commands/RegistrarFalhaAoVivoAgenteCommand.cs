using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// O agente não conseguiu transmitir (senha recusada, câmera sem resposta...): encerra a sessão com o código da falha, que a
// tela mostra pelo código. Rota anônima autenticada pelo segredo. Devolve false quando o agente não se autentica (401);
// sessão inexistente ou de outro agente = KeyNotFoundException (404). Sessão já encerrada: nada a fazer (idempotente).
// Motivo (texto livre do agente) é recebido mas NÃO é gravado: o código estável basta e o texto poderia carregar endereço
// ou credencial. Só códigos conhecidos (CodigosTesteConexaoCamera) são guardados.
public record RegistrarFalhaAoVivoAgenteCommand(
    Guid AgenteId,
    string? Segredo,
    string? VersaoAgente,
    Guid SessaoId,
    string? Codigo,
    string? Motivo) : IRequest<bool>;

public class RegistrarFalhaAoVivoAgenteCommandHandler : IRequestHandler<RegistrarFalhaAoVivoAgenteCommand, bool>
{
    private readonly IAppDbContext _db;
    private readonly IAgenteCameraAutenticador _autenticador;
    private readonly ICurrentUserService _usuarioAtual;
    private readonly ILogger<RegistrarFalhaAoVivoAgenteCommandHandler> _logger;

    public RegistrarFalhaAoVivoAgenteCommandHandler(
        IAppDbContext db,
        IAgenteCameraAutenticador autenticador,
        ICurrentUserService usuarioAtual,
        ILogger<RegistrarFalhaAoVivoAgenteCommandHandler> logger)
    {
        _db = db;
        _autenticador = autenticador;
        _usuarioAtual = usuarioAtual;
        _logger = logger;
    }

    public async Task<bool> Handle(RegistrarFalhaAoVivoAgenteCommand request, CancellationToken ct)
    {
        var agente = await _autenticador.ValidarAsync(request.AgenteId, request.Segredo, ct);
        if (agente is null) return false;

        _usuarioAtual.DefinirEscopo(temAcessoGlobal: false, new[] { agente.ObraId });
        await ContatoAgenteCamera.RegistrarAsync(_db, agente, request.VersaoAgente, _logger, ct);

        IQueryable<Domain.Entidades.SessaoAoVivoCamera> Esta() => _db.SessoesAoVivoCamera.IgnoreQueryFilters()
            .Where(s => s.Id == request.SessaoId && s.Ativo && s.ObraId == agente.ObraId && s.AgenteCameraId == agente.Id);

        if (!await Esta().AsNoTracking().AnyAsync(ct))
            throw new KeyNotFoundException(RegistrarQuadroAoVivoAgenteCommandHandler.CodigoSessaoNaoEncontrada);

        await SessaoAoVivoCameraRegras.EncerrarAsync(
            Esta(), SessaoAoVivoCameraRegras.MotivoFalha, DateTime.UtcNow, ct, CodigosTesteConexaoCamera.Normalizar(false, request.Codigo));
        return true;
    }
}
