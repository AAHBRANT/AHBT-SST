using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AcoesPlano;

// Conclusão automática das ações do plano da ocorrência ligadas a treinamento, inspeção ou entrega
// de EPI (23/10/2026, etapa 2 do desenho docs/mockups/plano-acao-conclusao-v1.html). Regra do
// usuário: "se o plano de ação tem treinamento, só resolve quando acontecer o treinamento daquele
// item" — só o fato real e específico conclui a ação:
// - Treinamento: turma do curso escolhido, Concluída, realizada a partir da data da ocorrência, com
//   presença confirmada de todos os envolvidos (sem envolvidos: basta a turma concluída nesta obra).
// - Inspeção: inspeção do tipo escolhido, Concluída, na obra da ocorrência, a partir da data dela.
// - Entrega de EPI: entrega do EPI escolhido, a partir da data da ocorrência, assinada por cada
//   envolvido (sem envolvidos não há a quem entregar: fica pendente).
// Chamado ao abrir a ocorrência, ao finalizar a investigação e quando uma turma ou inspeção é
// encerrada — idempotente.
public static class ResolucaoVinculadaAcaoPlano
{
    public record Avaliacao(bool Atendida, string Texto, string Aguardando, string? Progresso);

    // Avalia as ações vinculadas pendentes ou não das ocorrências informadas.
    public static async Task<Dictionary<Guid, Avaliacao>> AvaliarAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> acidenteIds, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, Avaliacao>();
        if (acidenteIds.Count == 0) return resultado;

        var acoes = await db.AcoesPlano
            .Where(a => a.OrigemTipo == nameof(Acidente) && acidenteIds.Contains(a.OrigemId) && a.TipoResolucao != null)
            .Select(a => new
            {
                a.Id, a.OrigemId, a.TipoResolucao, a.ResolucaoCursoTreinamentoId, a.ResolucaoTipoInspecao, a.ResolucaoCatalogoEpiId,
                a.ResolucaoTemaInspecaoId, a.ResponsavelUsuarioId, a.CreatedAtUtc,
                Tema = a.ResolucaoTemaInspecao != null ? a.ResolucaoTemaInspecao.Nome : null,
                Responsavel = a.ResponsavelUsuario != null ? a.ResponsavelUsuario.Nome : null,
                Curso = a.ResolucaoCursoTreinamento != null ? a.ResolucaoCursoTreinamento.Nome : null,
                Epi = a.ResolucaoCatalogoEpi != null ? a.ResolucaoCatalogoEpi.Nome : null,
            })
            .ToListAsync(ct);
        if (acoes.Count == 0) return resultado;

        var ocorrencias = await db.Acidentes
            .Where(a => acidenteIds.Contains(a.Id))
            .Select(a => new { a.Id, a.ObraId, a.Data })
            .ToDictionaryAsync(a => a.Id, ct);
        var envolvidosPorOcorrencia = await AcidenteEnvolvidosSync.CarregarAsync(db, acidenteIds, ct);

        foreach (var acao in acoes)
        {
            if (!ocorrencias.TryGetValue(acao.OrigemId, out var ocorrencia)) continue;
            var desde = ocorrencia.Data.Date;
            var envolvidos = (envolvidosPorOcorrencia.GetValueOrDefault(acao.OrigemId) ?? new())
                .Select(e => e.TrabalhadorId).Distinct().ToList();

            Avaliacao avaliacao;
            switch (acao.TipoResolucao)
            {
                case TipoResolucaoAcaoPlano.Treinamento:
                {
                    var curso = acao.Curso ?? "o curso escolhido";
                    var presentes = await db.ParticipantesSessaoTreinamento
                        .Where(p => p.PresencaConfirmadaEm != null
                            && p.SessaoTreinamento!.CursoTreinamentoId == acao.ResolucaoCursoTreinamentoId
                            && p.SessaoTreinamento.Status == StatusSessaoTreinamento.Concluida
                            && p.SessaoTreinamento.DataRealizacao >= desde)
                        .Select(p => p.TrabalhadorId)
                        .Distinct()
                        .ToListAsync(ct);
                    // Sem envolvidos: vale uma turma do curso concluída nesta obra.
                    var turmaConcluida = await db.SessoesTreinamento.AnyAsync(s =>
                        s.CursoTreinamentoId == acao.ResolucaoCursoTreinamentoId
                        && s.ObraId == ocorrencia.ObraId
                        && s.Status == StatusSessaoTreinamento.Concluida
                        && s.DataRealizacao >= desde, ct);
                    var feitos = envolvidos.Count(presentes.Contains);
                    avaliacao = envolvidos.Count == 0
                        ? new Avaliacao(turmaConcluida, $"Ao concluir uma turma de {curso} nesta obra", $"Aguardando treinamento {curso}", null)
                        : new Avaliacao(feitos == envolvidos.Count,
                            $"Ao concluir uma turma de {curso} com os {envolvidos.Count} envolvido(s) presente(s)",
                            $"Aguardando treinamento {curso}",
                            $"{feitos} de {envolvidos.Count} envolvido(s) treinado(s)");
                    break;
                }
                case TipoResolucaoAcaoPlano.Inspecao:
                {
                    var tipo = acao.ResolucaoTipoInspecao == TipoInspecao.Tema
                        ? acao.Tema ?? "o tema escolhido"
                        : DescreverTipoInspecao(acao.ResolucaoTipoInspecao);
                    var responsavel = acao.Responsavel ?? "o responsável designado";
                    // Só vale a inspeção finalizada PELO responsável designado na ação e DEPOIS de a ação existir:
                    // inspeção antiga ou de outra pessoa não resolve (um técnico já concluiu o plano inteiro por engano).
                    var desdeAcao = acao.CreatedAtUtc > desde ? acao.CreatedAtUtc : desde;
                    var consulta = db.Inspecoes.Where(i =>
                        i.ObraId == ocorrencia.ObraId
                        && i.TipoInspecao == acao.ResolucaoTipoInspecao
                        && i.Status == StatusInspecao.Concluida
                        && i.Data >= desdeAcao
                        && acao.ResponsavelUsuarioId != null
                        && i.ResponsavelUsuarioId == acao.ResponsavelUsuarioId);
                    if (acao.ResolucaoTipoInspecao == TipoInspecao.Tema)
                        consulta = consulta.Where(i => i.ItemInspecionavel != null && i.ItemInspecionavel.TemaInspecaoId == acao.ResolucaoTemaInspecaoId);
                    var feita = await consulta.AnyAsync(ct);
                    avaliacao = new Avaliacao(feita, $"Quando {responsavel} finalizar a inspeção de {tipo} nesta obra", $"Aguardando inspeção de {tipo} por {responsavel}", null);
                    break;
                }
                case TipoResolucaoAcaoPlano.EntregaEpi:
                {
                    var epi = acao.Epi ?? "o EPI escolhido";
                    var entregas = await db.EntregasEpi
                        .Where(e => e.CatalogoEpiId == acao.ResolucaoCatalogoEpiId
                            && envolvidos.Contains(e.TrabalhadorId)
                            && e.DataEntrega >= desde)
                        .Select(e => new { e.Id, e.TrabalhadorId })
                        .ToListAsync(ct);
                    var entregaIds = entregas.Select(e => e.Id).ToList();
                    var assinadas = await db.DocumentosAssinatura
                        .Where(d => d.EntidadeTipo == "EntregaEpi" && entregaIds.Contains(d.EntidadeId) && d.Signatarios.Any())
                        .Select(d => d.EntidadeId)
                        .ToListAsync(ct);
                    var feitos = entregas.Where(e => assinadas.Contains(e.Id)).Select(e => e.TrabalhadorId).Distinct().Count();
                    avaliacao = envolvidos.Count == 0
                        ? new Avaliacao(false, $"Ao assinar a entrega de {epi} para os envolvidos", $"Aguardando entrega de {epi}",
                            "Informe os envolvidos da ocorrência")
                        : new Avaliacao(feitos == envolvidos.Count,
                            $"Ao assinar a entrega de {epi} para os {envolvidos.Count} envolvido(s)",
                            $"Aguardando entrega de {epi}",
                            $"{feitos} de {envolvidos.Count} entrega(s) assinada(s)");
                    break;
                }
                default:
                    continue;
            }
            resultado[acao.Id] = avaliacao;
        }

        return resultado;
    }

    // Conclui as ações vinculadas cujo fato já aconteceu. Devolve quantas concluiu.
    public static async Task<int> ConcluirAtendidasAsync(IAppDbContext db, IReadOnlyCollection<Guid> acidenteIds, CancellationToken ct)
    {
        var avaliacoes = await AvaliarAsync(db, acidenteIds, ct);
        var atendidas = avaliacoes.Where(a => a.Value.Atendida).Select(a => a.Key).ToList();
        if (atendidas.Count == 0) return 0;

        var acoes = await db.AcoesPlano
            .Where(a => atendidas.Contains(a.Id) && a.Status != StatusControleRisco.Concluido)
            .ToListAsync(ct);
        foreach (var acao in acoes)
        {
            acao.Status = StatusControleRisco.Concluido;
            acao.DataConclusao = DateTime.UtcNow;
            var texto = avaliacoes[acao.Id].Texto;
            acao.DescricaoConclusao = texto.StartsWith("Ao ", StringComparison.Ordinal)
                ? "Concluída automaticamente ao " + texto[3..] + "."
                : "Concluída automaticamente: " + char.ToLowerInvariant(texto[0]) + texto[1..] + ".";
        }
        if (acoes.Count > 0) await db.SaveChangesAsync(ct);
        return acoes.Count;
    }

    // Ocorrências (ainda abertas) com ação vinculada a este curso/tipo — para os eventos de
    // encerramento de turma e de inspeção só olharem o necessário.
    public static Task<List<Guid>> OcorrenciasComTreinamentoAsync(IAppDbContext db, Guid cursoId, CancellationToken ct)
        => db.AcoesPlano
            .Where(a => a.OrigemTipo == nameof(Acidente) && a.TipoResolucao == TipoResolucaoAcaoPlano.Treinamento
                && a.ResolucaoCursoTreinamentoId == cursoId && a.Status != StatusControleRisco.Concluido)
            .Select(a => a.OrigemId).Distinct().ToListAsync(ct);

    public static Task<List<Guid>> OcorrenciasComInspecaoAsync(IAppDbContext db, Guid obraId, TipoInspecao tipo, Guid? temaId, CancellationToken ct)
        => db.AcoesPlano
            .Where(a => a.OrigemTipo == nameof(Acidente) && a.TipoResolucao == TipoResolucaoAcaoPlano.Inspecao
                && a.ResolucaoTipoInspecao == tipo && a.Status != StatusControleRisco.Concluido
                && (tipo != TipoInspecao.Tema || a.ResolucaoTemaInspecaoId == temaId)
                && db.Acidentes.Any(x => x.Id == a.OrigemId && x.ObraId == obraId))
            .Select(a => a.OrigemId).Distinct().ToListAsync(ct);

    // Mesmos rótulos da tela (lib/api.ts: tipoInspecaoLabel).
    public static string DescreverTipoInspecao(TipoInspecao? tipo) => tipo switch
    {
        TipoInspecao.Obra => "Obra",
        TipoInspecao.Canteiro => "Canteiro de obras",
        TipoInspecao.Epi => "EPI",
        TipoInspecao.Epc => "EPC",
        TipoInspecao.Maquinas => "Máquinas",
        TipoInspecao.Ferramentas => "Ferramentas",
        TipoInspecao.Andaimes => "Andaimes",
        TipoInspecao.Escadas => "Escadas",
        TipoInspecao.Eletrica => "Instalações elétricas",
        TipoInspecao.Altura => "Trabalho em altura",
        TipoInspecao.EspacoConfinado => "Espaço confinado",
        TipoInspecao.Comportamental => "Comportamental",
        TipoInspecao.Terceiros => "Terceiros",
        TipoInspecao.Alojamento => "Alojamento",
        TipoInspecao.Veiculo => "Veículo",
        TipoInspecao.Tema => "Tema",
        _ => "tipo escolhido",
    };
}
