using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SessoesTreinamento;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PlanosIntegracao;

public record TurmaAgendada(Guid SessaoTreinamentoId, string CursoNome, DateTime Data, bool Nova);

// Agendamento automático dos treinamentos do funcionário que entra na obra (especificação de
// 21/10/2026, etapa 3). Ordem: Integração de Segurança e NR-06 primeiro (sem elas não há EPI), depois
// os demais cursos da matriz da função. Cada curso entra na próxima turma aberta da obra; sem turma,
// uma nova é criada com o padrão da NR e data sugerida — o técnico confirma ou ajusta a data depois.
public static class AgendaTreinamentosIntegracao
{
    // Dias úteis depois da admissão: Integração e NR-06 no 2º dia útil, os demais no 3º.
    public const int DiasUteisPrimeirasTurmas = 2;
    public const int DiasUteisDemaisTurmas = 3;

    private static readonly TimeZoneInfo FusoBrasilia = ObterFusoBrasilia();

    public static DateTime HojeBrasilia() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoBrasilia).Date;

    // Sábado e domingo não contam; feriados não são considerados (mesma regra de SuporteIaCalendario).
    public static DateTime SomarDiasUteis(DateTime data, int dias)
    {
        var resultado = data.Date;
        while (dias > 0)
        {
            resultado = resultado.AddDays(1);
            if (resultado.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) dias--;
        }
        return resultado;
    }

    public static async Task<List<TurmaAgendada>> AgendarAsync(
        IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero, Trabalhador trabalhador, CancellationToken ct)
    {
        var agendadas = new List<TurmaAgendada>();
        var funcaoNome = await db.Funcoes.Where(f => f.Id == trabalhador.FuncaoId).Select(f => f.Nome).FirstOrDefaultAsync(ct);
        // Mesma isenção de CalculadoraLiberacaoTerceirizado: o Técnico de Segurança é quem conduz as turmas.
        if (FuncaoSstClassifier.EhTecnicoSeguranca(funcaoNome)) return agendadas;

        var cursos = await db.CursosTreinamento.ToListAsync(ct);
        var integracao = cursos.FirstOrDefault(c => c.EhIntegracaoSeguranca);
        var nr6 = cursos.Where(c => c.AtendeNr6)
            .OrderBy(c => CatalogoNormasRegulamentadoras.NormalizarNumero(c.NormaReferencia) == "NR-06" ? 0 : 1)
            .FirstOrDefault();
        var idsDaFuncao = await db.MatrizTreinamentoFuncoes
            .Where(m => m.FuncaoId == trabalhador.FuncaoId)
            .Select(m => m.CursoTreinamentoId)
            .ToListAsync(ct);

        var ordem = new List<(CursoTreinamento Curso, bool Primeira)>();
        if (integracao is not null) ordem.Add((integracao, true));
        if (nr6 is not null && nr6.Id != integracao?.Id) ordem.Add((nr6, true));
        foreach (var curso in cursos.Where(c => idsDaFuncao.Contains(c.Id) && ordem.All(o => o.Curso.Id != c.Id)).OrderBy(c => c.Nome))
            ordem.Add((curso, false));
        if (ordem.Count == 0) return agendadas;

        var hoje = HojeBrasilia();
        var idsCursos = ordem.Select(o => o.Curso.Id).ToList();
        var validos = await db.Treinamentos
            .Where(t => t.TrabalhadorId == trabalhador.Id && idsCursos.Contains(t.CursoTreinamentoId) && t.DataValidade.Date >= hoje)
            .Select(t => t.CursoTreinamentoId)
            .ToListAsync(ct);
        var jaInscrito = await db.ParticipantesSessaoTreinamento
            .Where(p => p.TrabalhadorId == trabalhador.Id && p.Ativo && p.SessaoTreinamento!.Ativo
                && p.SessaoTreinamento.Status == StatusSessaoTreinamento.EmAndamento)
            .Select(p => p.SessaoTreinamento!.CursoTreinamentoId)
            .ToListAsync(ct);
        var obraNome = await db.Obras.Where(o => o.Id == trabalhador.ObraId).Select(o => o.Nome).FirstOrDefaultAsync(ct);

        foreach (var (curso, primeira) in ordem.Where(o => !validos.Contains(o.Curso.Id) && !jaInscrito.Contains(o.Curso.Id)))
        {
            var turma = await db.SessoesTreinamento
                .Where(s => s.ObraId == trabalhador.ObraId && s.CursoTreinamentoId == curso.Id
                    && s.Status == StatusSessaoTreinamento.EmAndamento && s.DataRealizacao >= hoje)
                .OrderBy(s => s.DataRealizacao)
                .FirstOrDefaultAsync(ct);

            var nova = turma is null;
            if (turma is null)
            {
                var padrao = PadraoTurmaTreinamento.De(curso, obraNome);
                turma = new SessaoTreinamento
                {
                    ObraId = trabalhador.ObraId,
                    CursoTreinamentoId = curso.Id,
                    DataRealizacao = SomarDiasUteis(hoje, primeira ? DiasUteisPrimeirasTurmas : DiasUteisDemaisTurmas),
                    CargaHorariaRealizada = padrao.CargaHorariaMinima > 0 ? padrao.CargaHorariaMinima : Math.Max(1, curso.CargaHorariaMinima),
                    ConteudoProgramatico = padrao.ConteudoProgramatico,
                    Local = padrao.Local,
                    ValidadeEmMeses = padrao.ValidadeEmMeses,
                    NumeroCertificado = await geradorNumero.GerarAsync("CERT", ct),
                };
                db.SessoesTreinamento.Add(turma);
            }

            db.ParticipantesSessaoTreinamento.Add(new ParticipanteSessaoTreinamento { SessaoTreinamentoId = turma.Id, TrabalhadorId = trabalhador.Id });
            agendadas.Add(new TurmaAgendada(turma.Id, curso.Nome, turma.DataRealizacao, nova));
        }

        return agendadas;
    }

    private static TimeZoneInfo ObterFusoBrasilia()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }
}
