using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Elegibilidade.Rules;

// §45 da Base de Conhecimento — checa se o trabalhador tem ao menos um Treinamento dentro
// da validade. Ainda não há tabela de vínculo Atividade/TipoAutorizacao -> CursoTreinamento
// exigido (TipoAutorizacaoRequisito, deliberadamente adiado no plano da FASE D até existir
// mais de 2 regras) — por ora a checagem é genérica: "algum treinamento válido", não
// "o treinamento específico exigido para esta atividade".
public class TreinamentoValidoRule : IEligibilityRule
{
    private readonly IAppDbContext _db;

    public string NomeRequisito => "Treinamento válido";

    public TreinamentoValidoRule(IAppDbContext db) => _db = db;

    public async Task<EligibilityCheckItem> AvaliarAsync(EligibilityRequest request, CancellationToken ct = default)
    {
        var possuiTreinamentoValido = await _db.Treinamentos
            .Where(t => t.TrabalhadorId == request.TrabalhadorId && t.DataValidade.Date >= DateTime.UtcNow.Date)
            .AnyAsync(ct);

        return new EligibilityCheckItem
        {
            Requisito = NomeRequisito,
            Atendido = possuiTreinamentoValido,
            Critico = true,
            Detalhe = possuiTreinamentoValido ? null : "Trabalhador não possui treinamento válido (dentro da validade)."
        };
    }
}
