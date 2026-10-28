using FluentValidation;

namespace AAHBRANT.SST.Application.Common;

// Regra do usuário (28/10): treinamento e entrega de EPI já aconteceram — não existe data futura.
// "Hoje" é o dia em Brasília, não o do servidor (UTC): depois das 21h locais o UTC já virou o dia
// seguinte e deixaria passar a data de amanhã.
public static class DataNaoFuturaExtensions
{
    private static readonly TimeZoneInfo FusoBrasilia = ObterFusoBrasilia();

    public static DateTime HojeBrasilia() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoBrasilia).Date;

    public static IRuleBuilderOptions<T, DateTime> NaoFutura<T>(this IRuleBuilder<T, DateTime> regra, string campo) =>
        regra.Must(data => data.Date <= HojeBrasilia())
            .WithMessage($"{campo} não pode ser uma data futura.");

    public static IRuleBuilderOptions<T, DateTime?> NaoFutura<T>(this IRuleBuilder<T, DateTime?> regra, string campo) =>
        regra.Must(data => data is null || data.Value.Date <= HojeBrasilia())
            .WithMessage($"{campo} não pode ser uma data futura.");

    private static TimeZoneInfo ObterFusoBrasilia()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }
}
