using AAHBRANT.SST.Application.PlanosIntegracao;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

public sealed record PeriodoAutomatico(DateTime Data, TimeSpan Inicio, TimeSpan Fim, int Minutos);

// Carga horária automática (decisão do usuário, 16/11/2026): ao abrir a etapa de curso o sistema lança sozinho o período:
// data = hoje (Brasília), início = hora da abertura (no minuto), fim = início + carga prevista do curso. O fim pode estar no
// futuro (o Avançar espera por ele). Se a janela cruza a meia-noite, divide em um período por dia: o primeiro termina às
// 23:59 mas conta os minutos até a meia-noite, para a soma bater com a carga prevista.
public static class PeriodosAutomaticosEtapa
{
    public static readonly TimeSpan FimDoDia = new(23, 59, 0);

    public static IReadOnlyList<PeriodoAutomatico> Calcular(DateTime inicioUtc, int cargaHoras)
    {
        if (cargaHoras <= 0) return [];
        var brasilia = AgendaTreinamentosIntegracao.HoraBrasilia(inicioUtc);
        var atual = new DateTime(brasilia.Year, brasilia.Month, brasilia.Day, brasilia.Hour, brasilia.Minute, 0);
        var fim = atual.AddHours(cargaHoras);

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

    // Instante (horário de Brasília) em que o período termina de fato: início + minutos reais.
    public static DateTime FimReal(DateTime data, TimeSpan inicio, int minutos) => data.Date.Add(inicio).AddMinutes(minutos);
}
