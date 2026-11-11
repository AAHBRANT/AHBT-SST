using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

// "Integração realizada" (registro isolado no perfil, sem certificado): cada pessoa recebe quando a OS dela da obra fica
// válida (funcionário e técnico assinaram) DEPOIS do início de um atendimento concluído em que ela participou. Vale
// no "Concluir integração" e também quando a OS pendente é assinada depois, pelo perfil (hook em FinalizarDocumentoCommand).
public static class RegistroIntegracaoSincronizador
{
    // Cria o registro de cada participante cuja OS já está válida. Devolve quantos criou. Não faz SaveChanges.
    public static async Task<int> SincronizarAtendimentoAsync(IAppDbContext db, IntegracaoAtendimento at, CancellationToken ct)
    {
        var participantes = await db.ParticipantesIntegracao.Where(p => p.IntegracaoAtendimentoId == at.Id).ToListAsync(ct);
        if (participantes.Count == 0) return 0;
        var ids = participantes.Select(p => p.TrabalhadorId).ToList();

        var jaTem = (await db.RegistrosIntegracao.IgnoreQueryFilters()
            .Where(r => r.IntegracaoAtendimentoId == at.Id && r.Ativo).Select(r => r.TrabalhadorId).ToListAsync(ct)).ToHashSet();
        var pendentes = participantes.Where(p => !jaTem.Contains(p.TrabalhadorId)).ToList();
        if (pendentes.Count == 0) return 0;

        // Q9: quem ainda precisa de algum curso exigido (ex.: marcado "Não assinou" numa etapa) não recebe o registro; ele sai
        // quando tiver todos os cursos válidos e a OS assinada.
        var idsCand = pendentes.Select(p => p.TrabalhadorId).ToList();
        var pessoas = await db.Trabalhadores.IgnoreQueryFilters().Where(t => idsCand.Contains(t.Id)).ToListAsync(ct);
        var calculo = CalculadoraEtapasIntegracao.Calcular(
            await ExigenciasEtapasIntegracao.CarregarPessoasAsync(db, pessoas, at.ObraId, ct),
            new Dictionary<TipoEtapaIntegracao, CursoEtapaIntegracao>());
        var devemCurso = calculo.Where(e => e.Tipo != TipoEtapaIntegracao.OrdemServico).SelectMany(e => e.Participantes).ToHashSet();
        pendentes = pendentes.Where(p => !devemCurso.Contains(p.TrabalhadorId)).ToList();
        if (pendentes.Count == 0) return 0;

        var idsPendentes = pendentes.Select(p => p.TrabalhadorId).ToList();
        var ordens = await db.OrdensServico.IgnoreQueryFilters()
            .Where(o => o.Ativo && o.ObraId == at.ObraId && idsPendentes.Contains(o.TrabalhadorId)).ToListAsync(ct);
        if (ordens.Count == 0) return 0;
        var assinaturas = await AssinaturasOrdemServico.CarregarAsync(db, ordens, ct);

        var tecnicoNome = at.ResponsavelUsuarioId is { } uid
            ? await db.Usuarios.IgnoreQueryFilters().Where(u => u.Id == uid).Select(u => u.Nome).FirstOrDefaultAsync(ct)
            : null;

        var criados = 0;
        foreach (var p in pendentes)
        {
            var vigente = ordens.Where(o => o.TrabalhadorId == p.TrabalhadorId).OrderByDescending(o => o.Revisao).FirstOrDefault();
            if (vigente is null) continue;
            var (_, funcionario, tecnico) = assinaturas[vigente.Id];
            if (funcionario is null || tecnico is null || funcionario.AssinadoEm < at.IniciadaEmUtc) continue;

            p.OrdemServicoId = vigente.Id;
            db.RegistrosIntegracao.Add(new RegistroIntegracao
            {
                ObraId = at.ObraId,
                TrabalhadorId = p.TrabalhadorId,
                IntegracaoAtendimentoId = at.Id,
                RealizadaEm = DateTime.UtcNow,
                TecnicoUsuarioId = at.ResponsavelUsuarioId,
                TecnicoNome = string.IsNullOrWhiteSpace(tecnicoNome) ? "Técnico de Segurança" : tecnicoNome!,
                FuncaoNome = p.FuncaoNome,
            });
            criados++;
        }
        return criados;
    }

    // Gancho da assinatura da OS: se a pessoa estava num atendimento já concluído, completa o registro dela.
    public static async Task SincronizarPorOrdemServicoAsync(IAppDbContext db, Guid ordemServicoId, CancellationToken ct)
    {
        var os = await db.OrdensServico.IgnoreQueryFilters().Where(o => o.Id == ordemServicoId)
            .Select(o => new { o.TrabalhadorId, o.ObraId }).FirstOrDefaultAsync(ct);
        if (os is null) return;
        await SincronizarPorTrabalhadorAsync(db, os.TrabalhadorId, os.ObraId, ct);
    }

    // Também chamado quando a pessoa conclui um curso (turma avulsa): se faltava só o curso, o registro sai agora.
    public static async Task SincronizarPorTrabalhadorAsync(IAppDbContext db, Guid trabalhadorId, Guid obraId, CancellationToken ct)
    {
        var os = new { TrabalhadorId = trabalhadorId, ObraId = obraId };

        var atendimentoIds = await db.ParticipantesIntegracao.IgnoreQueryFilters()
            .Where(p => p.Ativo && p.TrabalhadorId == os.TrabalhadorId && p.ObraId == os.ObraId).Select(p => p.IntegracaoAtendimentoId).ToListAsync(ct);
        if (atendimentoIds.Count == 0) return;
        var atendimentos = await db.IntegracoesAtendimento.IgnoreQueryFilters()
            .Where(a => a.Ativo && atendimentoIds.Contains(a.Id) && a.Status == StatusIntegracaoAtendimento.Concluido).ToListAsync(ct);
        var criou = 0;
        foreach (var at in atendimentos)
            criou += await SincronizarAtendimentoAsync(db, at, ct);
        if (criou > 0) await db.SaveChangesAsync(ct);
    }
}
