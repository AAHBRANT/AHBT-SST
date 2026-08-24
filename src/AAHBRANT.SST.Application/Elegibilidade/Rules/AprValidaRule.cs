using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Elegibilidade.Rules;

// Extensão nova (não citada literalmente no §45, que só nomeia ASO/treinamento/autorização/
// análise de risco/inspeção/documentação como exemplos de requisito): usa §46 ("Análise de
// risco" precede "Liberação da atividade") para bloquear a atividade se não houver APR
// aprovada e vigente cobrindo o AtividadeId informado. Diferente de AsoValidoRule/
// TreinamentoValidoRule (que ignoram AtividadeId), esta regra só se aplica quando
// AtividadeId é informado — se nulo, é tratada como não-aplicável (não bloqueia).
public class AprValidaRule : IEligibilityRule
{
    private readonly IAppDbContext _db;

    public string NomeRequisito => "APR aprovada e vigente";

    public AprValidaRule(IAppDbContext db) => _db = db;

    public async Task<EligibilityCheckItem> AvaliarAsync(EligibilityRequest request, CancellationToken ct = default)
    {
        if (request.AtividadeId is null)
        {
            return new EligibilityCheckItem
            {
                Requisito = NomeRequisito,
                Atendido = true,
                Critico = false,
                Detalhe = "Não aplicável: nenhuma atividade informada nesta avaliação."
            };
        }

        var possuiAprValida = await _db.Aprs
            .Where(a => a.AtividadeId == request.AtividadeId.Value && a.Status == StatusApr.Aprovada)
            .AnyAsync(a => a.Validade == null || a.Validade.Value.Date >= DateTime.UtcNow.Date, ct);

        return new EligibilityCheckItem
        {
            Requisito = NomeRequisito,
            Atendido = possuiAprValida,
            Critico = true,
            Detalhe = possuiAprValida ? null : "Não há Análise Preliminar de Risco aprovada e vigente para esta atividade."
        };
    }
}
