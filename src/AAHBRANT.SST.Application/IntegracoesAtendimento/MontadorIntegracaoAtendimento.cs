using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Application.SessoesTreinamento;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

// Monta o retrato do atendimento (etapas, participantes, períodos, presenças e o que falta). Só consultas simples por
// tabela: nenhuma navegação obrigatória, para o histórico não sumir com registro excluído. O escopo por obra vem dos
// filtros globais das entidades do atendimento (outro técnico recebe null = 404).
public static class MontadorIntegracaoAtendimento
{
    public static async Task<IntegracaoAtendimentoDto?> MontarAsync(IAppDbContext db, Guid atendimentoId, CancellationToken ct)
    {
        var at = await db.IntegracoesAtendimento.FirstOrDefaultAsync(a => a.Id == atendimentoId, ct);
        if (at is null) return null;

        var etapas = await db.IntegracaoEtapas.Where(e => e.IntegracaoAtendimentoId == at.Id).OrderBy(e => e.Ordem).ToListAsync(ct);
        var participantes = await db.ParticipantesIntegracao.Where(p => p.IntegracaoAtendimentoId == at.Id).OrderBy(p => p.Nome).ToListAsync(ct);
        var nomes = participantes.ToDictionary(p => p.TrabalhadorId, p => p.Nome);
        var etapaIds = etapas.Select(e => e.Id).ToList();
        var periodos = await db.PeriodosEtapaIntegracao.Where(p => etapaIds.Contains(p.IntegracaoEtapaId))
            .OrderBy(p => p.Data).ThenBy(p => p.Inicio).ToListAsync(ct);
        var marcadosTodos = await db.NaoAssinouEtapasIntegracao.Where(n => etapaIds.Contains(n.IntegracaoEtapaId)).ToListAsync(ct);
        var sessaoIds = etapas.Where(e => e.SessaoTreinamentoId != null).Select(e => e.SessaoTreinamentoId!.Value).ToList();
        var sessoes = await db.SessoesTreinamento.Where(s => sessaoIds.Contains(s.Id)).ToListAsync(ct);
        var partSessao = await db.ParticipantesSessaoTreinamento
            .Where(p => sessaoIds.Contains(p.SessaoTreinamentoId) && p.Ativo)
            .Select(p => new { p.SessaoTreinamentoId, p.TrabalhadorId, p.PresencaConfirmadaEm, p.MetodoPresenca })
            .ToListAsync(ct);
        var fotos = await db.FotosEvidenciaSessaoTreinamento
            .Where(f => sessaoIds.Contains(f.SessaoTreinamentoId) && f.Ativo)
            .Select(f => f.SessaoTreinamentoId)
            .ToListAsync(ct);

        var idsPessoas = participantes.Select(p => p.TrabalhadorId).ToList();
        var comDigital = (await db.TemplatesBiometricoFutronic.Where(b => idsPessoas.Contains(b.TrabalhadorId))
            .Select(b => b.TrabalhadorId).Distinct().ToListAsync(ct)).ToHashSet();
        var comFacial = (await db.FotosCadastroFacial.Where(f => idsPessoas.Contains(f.TrabalhadorId))
            .Select(f => f.TrabalhadorId).Distinct().ToListAsync(ct)).ToHashSet();

        var obraNome = await db.Obras.Where(o => o.Id == at.ObraId).Select(o => o.Nome).FirstOrDefaultAsync(ct) ?? string.Empty;
        var instrutorNome = await db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => t.Id == at.InstrutorTrabalhadorId).Select(t => t.Nome).FirstOrDefaultAsync(ct);

        var responsavelNome = at.ResponsavelUsuarioId is { } uid
            ? await db.Usuarios.IgnoreQueryFilters().Where(u => u.Id == uid).Select(u => u.Nome).FirstOrDefaultAsync(ct)
            : null;

        // Conteúdo do curso e para quem vale (NR-12/NR-35 pelas exigências da obra; NR-06/NR-18 e OS são de todos).
        var cursoIdsEtapas = etapas.Where(e => e.CursoTreinamentoId != null).Select(e => e.CursoTreinamentoId!.Value).ToList();
        var cursosEtapas = await db.CursosTreinamento.Where(c => cursoIdsEtapas.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var funcoesObra = await db.Funcoes.Select(f => new { f.Id, f.Nome }).ToListAsync(ct);
        var exigencias = await AAHBRANT.SST.Application.CursosObrigatorios.ExigenciasTreinamento.CarregarAsync(db, [at.ObraId], ct);

        var etapasDto = new List<EtapaIntegracaoDto>();
        foreach (var e in etapas)
        {
            var sessao = e.SessaoTreinamentoId is { } sid ? sessoes.FirstOrDefault(s => s.Id == sid) : null;
            var partsEtapa = sessao is null
                ? partSessao.Take(0).ToList()
                : partSessao.Where(p => p.SessaoTreinamentoId == sessao.Id).ToList();
            var minutos = periodos.Where(p => p.IntegracaoEtapaId == e.Id).Sum(p => p.Minutos);
            var nFotos = sessao is null ? 0 : fotos.Count(f => f == sessao.Id);

            var partDto = partsEtapa
                .Select(p => new ParticipanteEtapaIntegracaoDto(
                    p.TrabalhadorId, nomes.GetValueOrDefault(p.TrabalhadorId, "Participante"),
                    p.PresencaConfirmadaEm is not null, p.PresencaConfirmadaEm,
                    p.MetodoPresenca is null ? null : TextoDto.Camel(p.MetodoPresenca.Value.ToString()),
                    comDigital.Contains(p.TrabalhadorId), comFacial.Contains(p.TrabalhadorId)))
                .ToList();
            var marcadosEtapa = marcadosTodos.Where(n => n.IntegracaoEtapaId == e.Id).ToList();
            partDto.AddRange(marcadosEtapa.Select(n => new ParticipanteEtapaIntegracaoDto(
                n.TrabalhadorId, nomes.GetValueOrDefault(n.TrabalhadorId, "Participante"), false, null, null,
                comDigital.Contains(n.TrabalhadorId), comFacial.Contains(n.TrabalhadorId), true, n.Motivo)));
            partDto = partDto.OrderBy(p => p.Nome).ToList();

            var faltas = new List<string>();
            if (sessao is not null && e.Status == StatusEtapaIntegracao.EmAndamento && !(partsEtapa.Count == 0 && marcadosEtapa.Count > 0))
            {
                var entidades = partsEtapa.Select(p => new ParticipanteSessaoTreinamento
                    { TrabalhadorId = p.TrabalhadorId, PresencaConfirmadaEm = p.PresencaConfirmadaEm }).ToList();
                faltas = AvaliadorEtapaIntegracao.Faltas(e, sessao, entidades, nomes, minutos, nFotos);
            }

            etapasDto.Add(new EtapaIntegracaoDto(
                e.Id, e.Ordem, TextoDto.Camel(e.Tipo.ToString()), CalculadoraEtapasIntegracao.Titulo(e.Tipo),
                TextoDto.Camel(e.Status.ToString()), e.CursoTreinamentoId, e.SessaoTreinamentoId,
                e.CargaMinimaHoras, minutos, e.IniciadaEmUtc, e.ConcluidaEmUtc, nFotos,
                sessao is null ? 0 : AvaliadorEtapaIntegracao.FotosNecessarias,
                partDto,
                periodos.Where(p => p.IntegracaoEtapaId == e.Id)
                    .Select(p => new PeriodoEtapaIntegracaoDto(p.Id, p.Data.ToString("yyyy-MM-dd"), p.Inicio.ToString(@"hh\:mm"), p.Fim.ToString(@"hh\:mm"), p.Minutos))
                    .ToList(),
                faltas,
                ConteudoDe(e, cursosEtapas, obraNome),
                AplicabilidadeDe(e, funcoesObra.Select(f => (f.Id, f.Nome)).ToList(), exigencias, at.ObraId)));
        }

        // Q5: entrar/sair enquanto a primeira etapa de curso não tiver nenhuma assinatura.
        var primeiraComCurso = etapas.Where(e => e.Tipo != TipoEtapaIntegracao.OrdemServico && e.Status != StatusEtapaIntegracao.Pulada)
            .OrderBy(e => e.Ordem).FirstOrDefault();
        var podeEditar = at.Status == StatusIntegracaoAtendimento.EmAndamento
            && (primeiraComCurso is null
                || (primeiraComCurso.Status == StatusEtapaIntegracao.EmAndamento
                    && !partSessao.Any(p => p.SessaoTreinamentoId == primeiraComCurso.SessaoTreinamentoId && p.PresencaConfirmadaEm != null)));

        return new IntegracaoAtendimentoDto(
            at.Id, at.Numero, at.ObraId, obraNome, TextoDto.Camel(at.Status.ToString()), at.EtapaAtualOrdem,
            at.IniciadaEmUtc, at.ConcluidaEmUtc, at.ResponsavelUsuarioId, responsavelNome, at.InstrutorTrabalhadorId, instrutorNome, at.Versao,
            podeEditar,
            participantes.Select(p => new ParticipanteIntegracaoDto(p.TrabalhadorId, p.Nome, p.FuncaoId, p.FuncaoNome, p.PlanoIntegracaoId, p.OrdemServicoId)).ToList(),
            etapasDto);
    }

    private static string? ConteudoDe(IntegracaoEtapa e, Dictionary<Guid, CursoTreinamento> cursos, string obraNome) =>
        e.CursoTreinamentoId is { } id && cursos.TryGetValue(id, out var curso)
            ? PadraoTurmaTreinamento.De(curso, obraNome).ConteudoProgramatico
            : null;

    private static string? AplicabilidadeDe(
        IntegracaoEtapa e, List<(Guid Id, string Nome)> funcoes, Func<Guid, Guid, List<Guid>?> exigencias, Guid obraId)
    {
        // Etapa de todos (NR-06, NR-18 e OS): sem texto.
        if (e.Tipo is TipoEtapaIntegracao.OrdemServico or TipoEtapaIntegracao.Nr06 or TipoEtapaIntegracao.Nr18) return null;
        if (e.CursoTreinamentoId is not { } cursoId) return null;
        var nomes = funcoes.Where(f => exigencias(obraId, f.Id)?.Contains(cursoId) == true).Select(f => f.Nome).OrderBy(n => n).ToList();
        if (nomes.Count == 0) return "Vale para as funções que o PGR da obra exige.";
        var lista = nomes.Count == 1 ? nomes[0]
            : nomes.Count <= 6 ? string.Join(", ", nomes[..^1]) + " e " + nomes[^1]
            : string.Join(", ", nomes.Take(6)) + $" e mais {nomes.Count - 6}";
        return $"Vale para {lista}.";
    }
}
