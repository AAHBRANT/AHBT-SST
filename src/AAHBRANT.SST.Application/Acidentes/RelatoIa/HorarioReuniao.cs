namespace AAHBRANT.SST.Application.Acidentes.RelatoIa;

/// <summary>
/// Primeiro horário livre de 1 hora na agenda do organizador, em hora cheia, entre 8h e 17h e fora do
/// almoço (12h–13h). Eventos de dia inteiro não ocupam a agenda (são os lembretes do Motor de Alertas).
/// </summary>
public static class HorarioReuniao
{
    public static readonly TimeSpan Duracao = TimeSpan.FromHours(1);
    private static readonly int[] HorasCandidatas = { 8, 9, 10, 11, 13, 14, 15, 16 };

    public static DateTime? PrimeiroLivre(DateTime dia, IEnumerable<(DateTime Inicio, DateTime Fim, bool DiaInteiro)> eventos)
    {
        var ocupados = eventos.Where(e => !e.DiaInteiro).ToList();
        foreach (var hora in HorasCandidatas)
        {
            var inicio = dia.Date.AddHours(hora);
            var fim = inicio + Duracao;
            if (!ocupados.Any(e => e.Inicio < fim && e.Fim > inicio))
                return inicio;
        }
        return null;
    }
}
