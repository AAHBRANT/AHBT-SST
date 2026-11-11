using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Instrutores;
using AAHBRANT.SST.Application.SessoesTreinamento;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

// Abre uma etapa: a de curso vira uma SessaoTreinamento (padrão da NR, instrutor do atendimento, vínculo com o
// atendimento) com inscritos só os participantes DA ETAPA; a da OS só passa a EmAndamento. Não faz SaveChanges.
public static class AberturaEtapaIntegracao
{
    // Participantes de uma etapa de curso, recalculados na hora de abrir (quem já tem a NR válida não entra).
    public static async Task<List<Guid>> ParticipantesDaEtapaAsync(
        IAppDbContext db, IntegracaoAtendimento atendimento, IntegracaoEtapa etapa, CancellationToken ct)
    {
        var ativos = await db.ParticipantesIntegracao
            .Where(p => p.IntegracaoAtendimentoId == atendimento.Id).Select(p => p.TrabalhadorId).ToListAsync(ct);
        if (etapa.Tipo == TipoEtapaIntegracao.OrdemServico) return ativos;

        var trabalhadores = await db.Trabalhadores.IgnoreQueryFilters().Where(t => ativos.Contains(t.Id)).ToListAsync(ct);
        var pessoas = await ExigenciasEtapasIntegracao.CarregarPessoasAsync(db, trabalhadores, atendimento.ObraId, ct);
        var cursos = etapa.CursoTreinamentoId is { } cursoId
            ? new Dictionary<TipoEtapaIntegracao, CursoEtapaIntegracao> { [etapa.Tipo] = new(cursoId, etapa.CargaMinimaHoras) }
            : new Dictionary<TipoEtapaIntegracao, CursoEtapaIntegracao>();
        return CalculadoraEtapasIntegracao.Calcular(pessoas, cursos).First(e => e.Tipo == etapa.Tipo).Participantes.ToList();
    }

    public static async Task AbrirAsync(
        IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero, IntegracaoAtendimento atendimento,
        IntegracaoEtapa etapa, IReadOnlyCollection<Guid> participantes, CancellationToken ct)
    {
        etapa.Status = StatusEtapaIntegracao.EmAndamento;
        etapa.IniciadaEmUtc = DateTime.UtcNow;
        atendimento.EtapaAtualOrdem = etapa.Ordem;
        if (etapa.Tipo == TipoEtapaIntegracao.OrdemServico) return;

        var curso = await db.CursosTreinamento.FirstOrDefaultAsync(c => c.Id == etapa.CursoTreinamentoId, ct)
            ?? throw new InvalidOperationException($"O curso da etapa {CalculadoraEtapasIntegracao.Titulo(etapa.Tipo)} não está cadastrado no catálogo.");
        var obraNome = await db.Obras.Where(o => o.Id == atendimento.ObraId).Select(o => o.Nome).FirstOrDefaultAsync(ct);
        var padrao = PadraoTurmaTreinamento.De(curso, obraNome);

        var instrutor = await db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => t.Id == atendimento.InstrutorTrabalhadorId).Select(t => new { t.Id, t.Nome }).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("O instrutor do atendimento não foi encontrado.");
        var perfil = await db.PerfisInstrutor.FirstOrDefaultAsync(p => p.TrabalhadorId == instrutor.Id, ct);

        var sessao = new SessaoTreinamento
        {
            ObraId = atendimento.ObraId,
            CursoTreinamentoId = curso.Id,
            IntegracaoAtendimentoId = atendimento.Id,
            DataRealizacao = ExigenciasEtapasIntegracao.HojeBrasilia(),
            CargaHorariaRealizada = Math.Max(1, Math.Max(padrao.CargaHorariaMinima, etapa.CargaMinimaHoras)),
            ConteudoProgramatico = padrao.ConteudoProgramatico,
            Local = padrao.Local,
            ValidadeEmMeses = padrao.ValidadeEmMeses,
            InstrutorTrabalhadorId = instrutor.Id,
            InstituicaoInstrutor = instrutor.Nome,
            InstrutorRegistroProfissional = perfil is null ? null : DescricaoInstrutor.Registro(perfil.Conselho, perfil.NumeroRegistro, perfil.UfRegistro),
            NumeroCertificado = await geradorNumero.GerarAsync("CERT", ct),
        };
        // Quem foi desligado/ficou inativo até a abertura da etapa não é inscrito: entra como "Não assinou" (motivo "Desligado").
        var ids = participantes.Distinct().ToList();
        var inativos = (await db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => ids.Contains(t.Id) && (!t.Ativo || t.Situacao == AAHBRANT.SST.Domain.Enums.SituacaoTrabalhador.Desligado))
            .Select(t => t.Id).ToListAsync(ct)).ToHashSet();
        foreach (var trabalhadorId in ids)
        {
            if (inativos.Contains(trabalhadorId))
                db.NaoAssinouEtapasIntegracao.Add(new NaoAssinouEtapaIntegracao
                {
                    ObraId = atendimento.ObraId, IntegracaoEtapaId = etapa.Id, TrabalhadorId = trabalhadorId,
                    Motivo = "Desligado", MarcadoEmUtc = DateTime.UtcNow,
                });
            else
                sessao.Participantes.Add(new ParticipanteSessaoTreinamento { SessaoTreinamentoId = sessao.Id, TrabalhadorId = trabalhadorId });
        }
        db.SessoesTreinamento.Add(sessao);
        etapa.SessaoTreinamentoId = sessao.Id;
    }

    // Abre, a partir da ordem informada, a primeira etapa que tem gente (as de curso sem ninguém viram Puladas).
    // Devolve a etapa aberta (a OS nunca é pulada).
    public static async Task<IntegracaoEtapa> AbrirProximaAsync(
        IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero, IntegracaoAtendimento atendimento,
        IReadOnlyList<IntegracaoEtapa> etapas, int aPartirDaOrdem, CancellationToken ct)
    {
        foreach (var etapa in etapas.Where(e => e.Ordem >= aPartirDaOrdem && e.Status is StatusEtapaIntegracao.Pendente or StatusEtapaIntegracao.Pulada)
                     .OrderBy(e => e.Ordem))
        {
            var participantes = await ParticipantesDaEtapaAsync(db, atendimento, etapa, ct);
            if (etapa.Tipo != TipoEtapaIntegracao.OrdemServico && participantes.Count == 0)
            {
                etapa.Status = StatusEtapaIntegracao.Pulada;
                continue;
            }
            await AbrirAsync(db, geradorNumero, atendimento, etapa, participantes, ct);
            return etapa;
        }
        throw new InvalidOperationException("A integração não tem uma próxima etapa.");
    }
}
