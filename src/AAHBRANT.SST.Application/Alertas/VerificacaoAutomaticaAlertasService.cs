using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas;

// Geração automática de alertas de vencimento (ASO, Treinamento, EPI, Documento de Gestão) e
// escalonamento automático de alertas parados (DataLimiteTratamento vencido) — a parte que
// CriarAlertaCommand/EscalonarAlertaCommand deliberadamente deixaram de fora (ver comentários
// desses arquivos: "dependeria de um job agendado que ainda não existe"). Chamado pelo
// AAHBRANT.SST.Worker em ciclo (ver VerificacaoAlertasBackgroundService lá).
//
// Fica na Application (não no Worker) porque é regra de negócio pura sobre IAppDbContext, sem
// nenhuma dependência de hosting — mesmo motivo dos Commands que já vivem aqui.
public class VerificacaoAutomaticaAlertasService
{
    private readonly IAppDbContext _db;

    public VerificacaoAutomaticaAlertasService(IAppDbContext db)
    {
        _db = db;
    }

    private sealed record CandidatoAlerta(
        Guid EntidadeId,
        Guid? TrabalhadorId,
        Guid? ObraId,
        bool JaVencido,
        DateTime DataLimite);

    public async Task<int> VerificarVencimentosAsync(int diasAntecedencia, CancellationToken ct)
    {
        var hoje = DateTime.UtcNow.Date;
        var limite = hoje.AddDays(diasAntecedencia);

        var total = 0;
        total += await ProcessarAsosAsync(hoje, limite, ct);
        total += await ProcessarTreinamentosAsync(hoje, limite, ct);
        total += await ProcessarEntregasEpiAsync(hoje, limite, ct);
        total += await ProcessarDocumentosAsync(hoje, limite, ct);
        return total;
    }

    private async Task<int> ProcessarAsosAsync(DateTime hoje, DateTime limite, CancellationToken ct)
    {
        var brutos = await _db.Asos
            .Where(a => a.Ativo && a.DataValidade <= limite)
            .Select(a => new { a.Id, a.TrabalhadorId, ObraId = a.Trabalhador!.ObraId, a.DataValidade })
            .ToListAsync(ct);

        var candidatos = brutos.Select(a => new CandidatoAlerta(
            a.Id, a.TrabalhadorId, a.ObraId, a.DataValidade < hoje, a.DataValidade));

        return await CriarAlertasFaltantesAsync(
            "Aso", candidatos, TipoAlerta.AsoVencido, TipoAlerta.AsoVencendo, "ASO", ct);
    }

    private async Task<int> ProcessarTreinamentosAsync(DateTime hoje, DateTime limite, CancellationToken ct)
    {
        var brutos = await _db.Treinamentos
            .Where(t => t.Ativo && t.DataValidade <= limite)
            .Select(t => new { t.Id, t.TrabalhadorId, ObraId = t.Trabalhador!.ObraId, t.DataValidade })
            .ToListAsync(ct);

        var candidatos = brutos.Select(t => new CandidatoAlerta(
            t.Id, t.TrabalhadorId, t.ObraId, t.DataValidade < hoje, t.DataValidade));

        return await CriarAlertasFaltantesAsync(
            "Treinamento", candidatos, TipoAlerta.TreinamentoVencido, TipoAlerta.TreinamentoVencendo, "Treinamento", ct);
    }

    private async Task<int> ProcessarEntregasEpiAsync(DateTime hoje, DateTime limite, CancellationToken ct)
    {
        // DataDevolucao preenchida = EPI já devolvido, não faz mais sentido alertar validade dele.
        var brutos = await _db.EntregasEpi
            .Where(e => e.Ativo && e.DataDevolucao == null && e.DataValidade != null && e.DataValidade <= limite)
            .Select(e => new { e.Id, e.TrabalhadorId, ObraId = e.Trabalhador!.ObraId, DataValidade = e.DataValidade!.Value })
            .ToListAsync(ct);

        var candidatos = brutos.Select(e => new CandidatoAlerta(
            e.Id, e.TrabalhadorId, e.ObraId, e.DataValidade < hoje, e.DataValidade));

        return await CriarAlertasFaltantesAsync(
            "EntregaEpi", candidatos, TipoAlerta.EpiVencido, TipoAlerta.EpiValidadeProxima, "EPI", ct);
    }

    private async Task<int> ProcessarDocumentosAsync(DateTime hoje, DateTime limite, CancellationToken ct)
    {
        var brutos = await _db.DocumentosGestao
            .Where(d => d.Ativo && d.Status == StatusDocumentoGestao.Vigente
                && d.Validade != null && d.Validade <= limite)
            .Select(d => new { d.Id, d.ObraId, DataValidade = d.Validade!.Value })
            .ToListAsync(ct);

        var candidatos = brutos.Select(d => new CandidatoAlerta(
            d.Id, TrabalhadorId: null, d.ObraId, d.DataValidade < hoje, d.DataValidade));

        return await CriarAlertasFaltantesAsync(
            "DocumentoGestao", candidatos, TipoAlerta.DocumentoVencido, TipoAlerta.DocumentoVencendo, "Documento", ct);
    }

    private async Task<int> CriarAlertasFaltantesAsync(
        string entidadeOrigemTipo,
        IEnumerable<CandidatoAlerta> candidatos,
        TipoAlerta tipoVencido,
        TipoAlerta tipoVencendo,
        string rotulo,
        CancellationToken ct)
    {
        var lista = candidatos.ToList();
        if (lista.Count == 0)
        {
            return 0;
        }

        var idsCandidatos = lista.Select(c => c.EntidadeId).ToList();
        var idsComAlertaAtivo = (await _db.Alertas
            .Where(a => a.EntidadeOrigemTipo == entidadeOrigemTipo
                && idsCandidatos.Contains(a.EntidadeOrigemId)
                && a.Status != StatusAlerta.Resolvido
                && a.Status != StatusAlerta.Ignorado)
            .Select(a => a.EntidadeOrigemId)
            .ToListAsync(ct))
            .ToHashSet();

        var novos = lista
            .Where(c => !idsComAlertaAtivo.Contains(c.EntidadeId))
            .Select(c => new Alerta
            {
                Tipo = c.JaVencido ? tipoVencido : tipoVencendo,
                Severidade = c.JaVencido ? SeveridadeAlerta.Critico : SeveridadeAlerta.Atencao,
                Titulo = c.JaVencido
                    ? $"{rotulo} vencido em {c.DataLimite:dd/MM/yyyy}"
                    : $"{rotulo} vence em {c.DataLimite:dd/MM/yyyy}",
                EntidadeOrigemTipo = entidadeOrigemTipo,
                EntidadeOrigemId = c.EntidadeId,
                TrabalhadorId = c.TrabalhadorId,
                ObraId = c.ObraId,
                DataLimiteTratamento = c.DataLimite,
            })
            .ToList();

        if (novos.Count == 0)
        {
            return 0;
        }

        _db.Alertas.AddRange(novos);
        await _db.SaveChangesAsync(ct);
        return novos.Count;
    }

    public async Task<int> EscalonarPendentesAsync(CancellationToken ct)
    {
        var agora = DateTime.UtcNow;

        var pendentes = await _db.Alertas
            .Where(a => a.DataLimiteTratamento != null
                && a.DataLimiteTratamento < agora
                && a.EscalonadoParaUsuarioId == null
                && a.Status != StatusAlerta.Resolvido
                && a.Status != StatusAlerta.Ignorado
                && a.Status != StatusAlerta.Escalonado)
            .ToListAsync(ct);

        if (pendentes.Count == 0)
        {
            return 0;
        }

        var gestoresQsms = await _db.UsuariosPerfilObra
            .Where(v => v.PerfilAcesso != null && v.PerfilAcesso.Tipo == TipoPerfilAcesso.GestorQsms
                && v.Usuario != null && v.Usuario.Status == StatusUsuario.Ativo)
            .Select(v => new { v.ObraId, v.UsuarioId })
            .ToListAsync(ct);

        // Prioriza um GestorQsms vinculado à obra específica do alerta; sem achar, cai para um com
        // escopo global (ObraId nulo em UsuarioPerfilObra) — leitura de docs/RBAC-Matrix.md §3
        // ("GestorQsms: G ou U... destino final do escalonamento automático").
        Guid? ResolverGestor(Guid? obraId)
        {
            var especifico = gestoresQsms.FirstOrDefault(g => g.ObraId == obraId);
            if (especifico is not null)
            {
                return especifico.UsuarioId;
            }

            var global = gestoresQsms.FirstOrDefault(g => g.ObraId == null);
            return global?.UsuarioId;
        }

        var escalonados = 0;
        foreach (var alerta in pendentes)
        {
            var gestorId = ResolverGestor(alerta.ObraId);
            if (gestorId is null)
            {
                // Nenhum GestorQsms cadastrado (nem para a obra, nem globalmente) — não há para
                // quem escalonar ainda; fica pendente até existir alguém com esse perfil.
                continue;
            }

            alerta.Status = StatusAlerta.Escalonado;
            alerta.EscalonadoParaUsuarioId = gestorId;
            alerta.DataEscalonamento = agora;
            escalonados++;
        }

        if (escalonados > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return escalonados;
    }
}
