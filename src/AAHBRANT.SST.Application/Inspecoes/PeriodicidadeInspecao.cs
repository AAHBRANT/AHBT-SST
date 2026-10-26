namespace AAHBRANT.SST.Application.Inspecoes;

// Inspeção periódica por item (veículo ou item de tema): vence N dias depois da última inspeção
// concluída — ou, se nunca houve, N dias depois do cadastro do item ("desde a última ou primeira").
public static class PeriodicidadeInspecao
{
    public const int MinimoDias = 1;
    public const int MaximoDias = 365;

    public static DateTime? ProximaEm(int? periodicidadeDias, DateTime? ultimaConcluidaEm, DateTime cadastradoEm)
        => periodicidadeDias is null ? null : (ultimaConcluidaEm ?? cadastradoEm).Date.AddDays(periodicidadeDias.Value);
}
