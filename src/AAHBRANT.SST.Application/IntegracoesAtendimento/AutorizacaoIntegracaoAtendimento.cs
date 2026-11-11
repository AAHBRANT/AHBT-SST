using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

public sealed record ResponsavelIntegracao(Guid? UsuarioId, string Nome);

// Quem conduz a integração de uma obra: Técnico de Segurança ou Administrador COM vínculo naquela obra (ou escopo
// global). Quem não tem acesso recebe 404 (KeyNotFoundException), como o resto do sistema: não confirma que o id
// existe. O filtro global de Obra (escopo por obra) já esconde obras de fora do escopo do usuário.
// Com o Entra ID desligado (desenvolvimento) libera, como PerfilTecnicoSeguranca.
public static class AutorizacaoIntegracaoAtendimento
{
    public static async Task<ResponsavelIntegracao> AutorizarAsync(
        IAppDbContext db, Guid obraId, string? azureAdObjectId, bool autenticacaoHabilitada, CancellationToken ct)
    {
        if (!await db.Obras.AnyAsync(o => o.Id == obraId, ct))
            throw new KeyNotFoundException("Integração não encontrada nesta obra.");

        if (!autenticacaoHabilitada)
            return new ResponsavelIntegracao(null, "Responsável");

        if (string.IsNullOrWhiteSpace(azureAdObjectId))
            throw new KeyNotFoundException("Integração não encontrada nesta obra.");

        var usuario = await db.Usuarios
            .Where(u => u.AzureAdObjectId == azureAdObjectId && u.Ativo && u.Status == StatusUsuario.Ativo)
            .Select(u => new { u.Id, u.Nome })
            .FirstOrDefaultAsync(ct);
        if (usuario is null)
            throw new KeyNotFoundException("Integração não encontrada nesta obra.");

        var autorizado = await db.UsuariosPerfilObra.AnyAsync(v =>
            v.Ativo && v.UsuarioId == usuario.Id
            && (v.ObraId == null || v.ObraId == obraId)
            && v.PerfilAcesso != null && v.PerfilAcesso.Ativo
            && (v.PerfilAcesso.Tipo == TipoPerfilAcesso.TecnicoSeguranca || v.PerfilAcesso.Tipo == TipoPerfilAcesso.Administrador), ct);
        if (!autorizado)
            throw new KeyNotFoundException("Integração não encontrada nesta obra.");

        return new ResponsavelIntegracao(usuario.Id, usuario.Nome);
    }

    // Carrega o atendimento (já no escopo de obras do usuário: filtro global) e confere o vínculo do técnico na obra dele.
    public static async Task<(IntegracaoAtendimento Atendimento, ResponsavelIntegracao Responsavel)> CarregarAsync(
        IAppDbContext db, Guid atendimentoId, string? azureAdObjectId, bool autenticacaoHabilitada, CancellationToken ct)
    {
        var atendimento = await db.IntegracoesAtendimento.FirstOrDefaultAsync(a => a.Id == atendimentoId, ct)
            ?? throw new KeyNotFoundException("Integração não encontrada nesta obra.");
        var responsavel = await AutorizarAsync(db, atendimento.ObraId, azureAdObjectId, autenticacaoHabilitada, ct);
        return (atendimento, responsavel);
    }
}
