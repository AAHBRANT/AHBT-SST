using AAHBRANT.SST.Domain.Interfaces;

namespace AAHBRANT.SST.Application.Elegibilidade;

// Implementação real do motor de elegibilidade/bloqueio preventivo (§45) — agrega todas as
// IEligibilityRule registradas via DI (Strategy pattern já desenhado na interface).
public class EligibilityService : IEligibilityService
{
    private readonly IEnumerable<IEligibilityRule> _regras;

    public EligibilityService(IEnumerable<IEligibilityRule> regras) => _regras = regras;

    public async Task<EligibilityResult> AvaliarAsync(EligibilityRequest request, CancellationToken ct = default)
    {
        var itens = new List<EligibilityCheckItem>();

        foreach (var regra in _regras)
            itens.Add(await regra.AvaliarAsync(request, ct));

        var itensNaoAtendidosCriticos = itens.Where(i => i.Critico && !i.Atendido).ToList();
        var liberado = itensNaoAtendidosCriticos.Count == 0;

        return new EligibilityResult
        {
            Liberado = liberado,
            Itens = itens,
            MotivoBloqueioResumo = liberado
                ? null
                : string.Join("; ", itensNaoAtendidosCriticos.Select(i => i.Detalhe ?? i.Requisito))
        };
    }
}
