using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Common.Seguranca;

// Quem pode aprovar cursos obrigatórios e atividades por função (17/11/2026): Técnico de Segurança,
// Engenheiro de Segurança OU Administrador, e só nas obras em que tem esse perfil (vínculo ativo na obra
// ou global; o Administrador segue o escopo de obras do próprio vínculo).
// A policy de perfil da API aceita o perfil em qualquer obra; este é o corte por obra. Aprovar o PGR NÃO
// passa por aqui (só Engenheiro) e a matriz de risco usa outra policy (Técnico ou Engenheiro, sem Administrador).
public static class DecisaoTecnicaNaObra
{
    public const string MensagemSemPerfil = "Só o técnico, o engenheiro de segurança ou o administrador desta obra pode confirmar esta decisão.";

    public static bool PerfilPodeDecidir(TipoPerfilAcesso tipo)
        => tipo is TipoPerfilAcesso.EngenheiroSeguranca or TipoPerfilAcesso.TecnicoSeguranca or TipoPerfilAcesso.Administrador;

    // exigir=false: Entra ID desligado (dev, sem identidade real) mantém o comportamento de dev.
    // Obra fora do escopo do usuário = 404 (não confirma que existe); na obra sem o perfil = 403.
    public static async Task GarantirAsync(IAppDbContext db, Guid obraId, Guid? usuarioId, bool exigir, CancellationToken ct)
    {
        if (!exigir) return;
        if (!db.ObraNoEscopo(obraId))
            throw new KeyNotFoundException($"Obra {obraId} não encontrada.");
        if (!await PodeNaObraAsync(db, obraId, usuarioId, ct))
            throw new AcessoNegadoException(MensagemSemPerfil);
    }

    public static async Task<bool> PodeNaObraAsync(IAppDbContext db, Guid obraId, Guid? usuarioId, CancellationToken ct)
    {
        if (usuarioId is not { } id) return false;
        return await db.UsuariosPerfilObra.AnyAsync(v => v.UsuarioId == id && v.Ativo
            && (v.ObraId == null || v.ObraId == obraId)
            && v.PerfilAcesso != null && v.PerfilAcesso.Ativo
            && (v.PerfilAcesso.Tipo == TipoPerfilAcesso.EngenheiroSeguranca
                || v.PerfilAcesso.Tipo == TipoPerfilAcesso.TecnicoSeguranca
                || v.PerfilAcesso.Tipo == TipoPerfilAcesso.Administrador), ct);
    }
}
