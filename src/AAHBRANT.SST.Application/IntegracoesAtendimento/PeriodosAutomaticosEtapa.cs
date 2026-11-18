using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.PlanosIntegracao;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

public sealed record PeriodoAutomatico(DateTime Data, TimeSpan Inicio, TimeSpan Fim, int Minutos);

// Carga horária automática (decisão do usuário, 16/11/2026; sem nenhum lançamento manual desde 17/11/2026): ao abrir a etapa de
// curso o sistema lança sozinho o período: data = hoje (Brasília), início = hora da abertura (no minuto), fim = início + carga
// prevista do curso. O fim pode estar no futuro (o Avançar espera por ele). Se a janela cruza a meia-noite, divide em um período
// por dia: o primeiro termina às 23:59 mas conta os minutos até a meia-noite, para a soma bater com a carga prevista.
public static class PeriodosAutomaticosEtapa
{
    public static readonly TimeSpan FimDoDia = new(23, 59, 0);

    // A carga automática entrou em 16/11/2026 (00:00 em Brasília = 03:00 UTC). Etapas abertas ANTES disso mantêm os períodos
    // manuais lançados (foram o treinamento real) e nunca são recontadas. A propriedade é settable só para os testes (que abrem
    // etapas "no passado"); em produção vale sempre a constante.
    public static readonly DateTime InicioDaRegraUtc = new(2026, 11, 16, 3, 0, 0, DateTimeKind.Utc);
    public static DateTime InicioDaRegraVigenteUtc { get; set; } = InicioDaRegraUtc;

    public static IReadOnlyList<PeriodoAutomatico> Calcular(DateTime inicioUtc, int cargaHoras) =>
        cargaHoras <= 0 ? [] : CalcularMinutos(inicioUtc, cargaHoras * 60);

    // Da abertura até "fimUtc" (no minuto; no mínimo 1 min). Usado no avanço de etapa sem carga prevista.
    public static IReadOnlyList<PeriodoAutomatico> CalcularAte(DateTime inicioUtc, DateTime fimUtc)
    {
        var ini = AgendaTreinamentosIntegracao.HoraBrasilia(inicioUtc);
        var fim = AgendaTreinamentosIntegracao.HoraBrasilia(fimUtc);
        var iniMin = new DateTime(ini.Year, ini.Month, ini.Day, ini.Hour, ini.Minute, 0);
        var fimMin = new DateTime(fim.Year, fim.Month, fim.Day, fim.Hour, fim.Minute, 0);
        return CalcularMinutos(inicioUtc, Math.Max(1, (int)(fimMin - iniMin).TotalMinutes));
    }

    public static IReadOnlyList<PeriodoAutomatico> CalcularMinutos(DateTime inicioUtc, int totalMinutos)
    {
        if (totalMinutos <= 0) return [];
        var brasilia = AgendaTreinamentosIntegracao.HoraBrasilia(inicioUtc);
        var atual = new DateTime(brasilia.Year, brasilia.Month, brasilia.Day, brasilia.Hour, brasilia.Minute, 0);
        var fim = atual.AddMinutes(totalMinutos);

        var periodos = new List<PeriodoAutomatico>();
        while (atual < fim)
        {
            var meiaNoite = atual.Date.AddDays(1);
            var parteFim = fim < meiaNoite ? fim : meiaNoite;
            var minutos = (int)(parteFim - atual).TotalMinutes;
            var fimDoPeriodo = parteFim == meiaNoite ? FimDoDia : parteFim.TimeOfDay;
            periodos.Add(new PeriodoAutomatico(atual.Date, atual.TimeOfDay, fimDoPeriodo, minutos));
            atual = parteFim;
        }
        return periodos;
    }

    // Autocorreção idempotente (17/11/2026): a carga da etapa de curso com carga prevista é SEMPRE a automática, contada desde a
    // abertura. Se os períodos atuais não forem exatamente os automáticos (algum manual, de antes desta regra, ou nenhum), troca-os
    // pelos calculados a partir de IniciadaEmUtc. Não checa choque com outra etapa: o tempo real é o da abertura.
    // Não faz SaveChanges; devolve true se mudou algo.
    public static async Task<bool> GarantirAsync(IAppDbContext db, IntegracaoAtendimento atendimento, IntegracaoEtapa etapa, CancellationToken ct, DateTime? inicioDaRegraUtc = null)
    {
        if (etapa.Tipo == TipoEtapaIntegracao.OrdemServico || etapa.Status != StatusEtapaIntegracao.EmAndamento
            || etapa.IniciadaEmUtc is not { } inicio || etapa.CargaMinimaHoras <= 0
            || inicio < (inicioDaRegraUtc ?? InicioDaRegraVigenteUtc))
            return false;

        var esperados = Calcular(inicio, etapa.CargaMinimaHoras);
        var atuais = await db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).ToListAsync(ct);
        var igual = atuais.Count == esperados.Count && esperados.All(e => atuais.Any(a =>
            a.Automatico && a.Data.Date == e.Data && a.Inicio == e.Inicio && a.Fim == e.Fim && a.Minutos == e.Minutos));
        if (igual) return false;

        db.PeriodosEtapaIntegracao.RemoveRange(atuais);
        foreach (var p in esperados) db.PeriodosEtapaIntegracao.Add(Novo(atendimento, etapa, p));
        return true;
    }

    // Etapa sem carga prevista (0): o período automático (abertura até agora) é calculado EM MEMÓRIA, para a avaliação do Avançar,
    // e só é gravado pelo Avançar que de fato conclui. Devolve null quando não se aplica (etapa de OS, carga > 0 ou com período manual).
    public static List<PeriodoEtapaIntegracao>? PeriodosCargaZeroAteAgora(
        IntegracaoAtendimento atendimento, IntegracaoEtapa etapa, IReadOnlyCollection<PeriodoEtapaIntegracao> atuais, DateTime? agoraUtc = null)
    {
        if (etapa.Tipo == TipoEtapaIntegracao.OrdemServico || etapa.Status != StatusEtapaIntegracao.EmAndamento
            || etapa.IniciadaEmUtc is not { } inicio || etapa.CargaMinimaHoras > 0 || atuais.Any(p => !p.Automatico))
            return null;
        return CalcularAte(inicio, agoraUtc ?? DateTime.UtcNow).Select(p => Novo(atendimento, etapa, p)).ToList();
    }

    public static PeriodoEtapaIntegracao Novo(IntegracaoAtendimento atendimento, IntegracaoEtapa etapa, PeriodoAutomatico p) => new()
    {
        ObraId = atendimento.ObraId, IntegracaoEtapaId = etapa.Id, Data = p.Data, Inicio = p.Inicio, Fim = p.Fim,
        Minutos = p.Minutos, Automatico = true,
    };

    // Instante (horário de Brasília) em que o período termina de fato: início + minutos reais.
    public static DateTime FimReal(DateTime data, TimeSpan inicio, int minutos) => data.Date.Add(inicio).AddMinutes(minutos);
}
