using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Queries;

// Aviso de falha silenciosa (pedido do usuário, 25/09/2026): o client secret do Graph ficou inválido
// em hml de 30/08 a 25/09 e nenhuma notificação chegou ao sininho sem ninguém perceber — os erros só
// existiam no log. A tela de Alertas consulta isto e avisa quando o envio mais recente falhou.
public record ObterSaudeEnvioTeamsQuery : IRequest<SaudeEnvioTeamsDto>;

public class SaudeEnvioTeamsDto
{
    public bool Falhando { get; set; }
    public int FalhasDesdeUltimoSucesso { get; set; }
    public DateTime? UltimaFalhaEmUtc { get; set; }
    public DateTime? UltimoSucessoEmUtc { get; set; }
    public string? UltimoErro { get; set; }
}

public class ObterSaudeEnvioTeamsQueryHandler : IRequestHandler<ObterSaudeEnvioTeamsQuery, SaudeEnvioTeamsDto>
{
    private const string CanalActivityFeed = "ActivityFeed";
    private const int TamanhoMaximoErro = 300;

    private readonly IAppDbContext _db;

    public ObterSaudeEnvioTeamsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<SaudeEnvioTeamsDto> Handle(ObterSaudeEnvioTeamsQuery request, CancellationToken ct)
    {
        var envios = _db.AlertaHistoricoEnvios.AsNoTracking().Where(h => h.Canal == CanalActivityFeed);

        var ultimoSucessoEm = await envios
            .Where(h => h.Sucesso)
            .MaxAsync(h => (DateTime?)h.CreatedAtUtc, ct);

        var falhasDepois = envios.Where(h => !h.Sucesso && (ultimoSucessoEm == null || h.CreatedAtUtc > ultimoSucessoEm));

        var ultimaFalha = await falhasDepois
            .OrderByDescending(h => h.CreatedAtUtc)
            .Select(h => new { h.CreatedAtUtc, h.MensagemErro })
            .FirstOrDefaultAsync(ct);

        if (ultimaFalha is null)
            return new SaudeEnvioTeamsDto { UltimoSucessoEmUtc = ultimoSucessoEm };

        var erro = ultimaFalha.MensagemErro;
        if (erro is { Length: > TamanhoMaximoErro })
            erro = erro[..TamanhoMaximoErro] + "…";

        return new SaudeEnvioTeamsDto
        {
            Falhando = true,
            FalhasDesdeUltimoSucesso = await falhasDepois.CountAsync(ct),
            UltimaFalhaEmUtc = ultimaFalha.CreatedAtUtc,
            UltimoSucessoEmUtc = ultimoSucessoEm,
            UltimoErro = erro,
        };
    }
}
