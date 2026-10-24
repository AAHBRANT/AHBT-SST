using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas;

// Helper reutilizável (módulo Terceirizado, docs/superpowers/specs/2026-09-18-modulo-terceirizado-
// design.md §8) — acha quem notificar quando um evento pontual da Obra precisa de atenção do
// Técnico de Segurança (falta de estoque de EPI, contrato encerrado com gente ainda ativa). Não usa
// o Motor Central de Alertas (AlertaEngineService/IAlertaOrigemProvider) porque esses eventos não
// são "vencimento" — são disparados no ato, não numa varredura periódica.
public interface ITecnicosSegurancaPorObraService
{
    Task<List<Guid>> ObterUsuarioIdsAsync(Guid obraId, CancellationToken ct = default);
}

public class TecnicosSegurancaPorObraService : ITecnicosSegurancaPorObraService
{
    private readonly IAppDbContext _db;
    public TecnicosSegurancaPorObraService(IAppDbContext db) => _db = db;

    public async Task<List<Guid>> ObterUsuarioIdsAsync(Guid obraId, CancellationToken ct = default)
        => await _db.UsuariosPerfilObra
            .Where(v => v.PerfilAcesso!.Tipo == TipoPerfilAcesso.TecnicoSeguranca)
            .Where(v => v.ObraId == null || v.ObraId == obraId)
            .Where(v => v.Usuario!.Status == StatusUsuario.Ativo)
            .Select(v => v.UsuarioId)
            .Distinct()
            .ToListAsync(ct);
}
