using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Elegibilidade.Rules;

// Extensão nova (mesmo padrão de AprValidaRule): usa §46 para bloquear a atividade se não
// houver PT autorizada e vigente cobrindo o AtividadeId informado. EligibilityRequest.
// PermissaoTrabalhoId já estava pré-declarado na interface aguardando este módulo, mas esta
// regra avalia por AtividadeId (como AprValidaRule), não por PermissaoTrabalhoId específico —
// o campo pré-declarado fica disponível para um uso futuro mais direcionado (ex.: validar uma
// PT específica antes de uma ação pontual), não usado aqui. Se AtividadeId for nulo, é
// tratada como não-aplicável (não bloqueia).
public class PermissaoTrabalhoValidaRule : IEligibilityRule
{
    private readonly IAppDbContext _db;

    public string NomeRequisito => "Permissão de Trabalho autorizada e vigente";

    public PermissaoTrabalhoValidaRule(IAppDbContext db) => _db = db;

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

        var possuiPtValida = await _db.PermissoesTrabalho
            .Where(p => p.AtividadeId == request.AtividadeId.Value && p.Status == StatusPt.Autorizada)
            .AnyAsync(p => p.Validade == null || p.Validade.Value.Date >= DateTime.UtcNow.Date, ct);

        return new EligibilityCheckItem
        {
            Requisito = NomeRequisito,
            Atendido = possuiPtValida,
            Critico = true,
            Detalhe = possuiPtValida ? null : "Não há Permissão de Trabalho autorizada e vigente para esta atividade."
        };
    }
}
