using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.AcoesPlano;

// Procedimento de Inspeção Técnica de Campo (§7) — prazos sugeridos para definição da ação,
// conforme a prioridade: Crítica = imediato/até 24h; Alta = até 48h; Média = até 5 dias úteis;
// Baixa = até 10 dias úteis. O documento chama estes prazos de "referência para parametrização" —
// por isso o cálculo aqui só sugere um valor inicial; CriarAcaoPlanoCommand aceita um Prazo
// explícito que sobrepõe a sugestão quando informado (ver uso em CriarAcaoPlanoCommand).
public static class SlaPrioridadeCalculator
{
    public static DateTime CalcularPrazoSugerido(PrioridadeAcao prioridade, DateTime dataBase) => prioridade switch
    {
        PrioridadeAcao.Critica => dataBase.AddHours(24),
        PrioridadeAcao.Alta => dataBase.AddHours(48),
        PrioridadeAcao.Media => AdicionarDiasUteis(dataBase, 5),
        PrioridadeAcao.Baixa => AdicionarDiasUteis(dataBase, 10),
        _ => dataBase.AddHours(48),
    };

    private static DateTime AdicionarDiasUteis(DateTime dataBase, int diasUteis)
    {
        var data = dataBase;
        var restantes = diasUteis;
        while (restantes > 0)
        {
            data = data.AddDays(1);
            if (data.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                restantes--;
        }
        return data;
    }
}
