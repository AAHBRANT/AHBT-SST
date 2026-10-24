namespace AAHBRANT.SST.Application.Acidentes.RelatoIa;

/// <summary>Datas no horário da obra (Brasília), não em UTC — "hoje" e "amanhã" do canteiro.</summary>
public static class CalendarioObra
{
    public static DateTime AgoraEmBrasilia()
    {
        TimeZoneInfo fuso;
        try { fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { fuso = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso);
    }

    // DDS do dia seguinte: sábado e domingo pulam para segunda. Feriados não são considerados — o
    // sistema não tem calendário de feriados (limitação avisada ao usuário em 08/10/2026).
    public static DateTime ProximoDiaUtil(DateTime dia)
    {
        var proximo = dia.Date.AddDays(1);
        while (proximo.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            proximo = proximo.AddDays(1);
        return proximo;
    }
}
