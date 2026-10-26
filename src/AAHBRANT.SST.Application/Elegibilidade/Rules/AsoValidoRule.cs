using AAHBRANT.SST.Application.Asos;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Elegibilidade.Rules;

// §45 da Base de Conhecimento — checa o ASO vigente do trabalhador (o do exame mais recente, ver AsoVigente):
// precisa estar com ResultadoStatus=Apto (ou AptoComRestricao) e dentro da validade.
public class AsoValidoRule : IEligibilityRule
{
    private readonly IAppDbContext _db;

    public string NomeRequisito => "ASO válido";

    public AsoValidoRule(IAppDbContext db) => _db = db;

    public async Task<EligibilityCheckItem> AvaliarAsync(EligibilityRequest request, CancellationToken ct = default)
    {
        var aso = AsoVigente.Eleger((await _db.Asos
                .Where(a => a.TrabalhadorId == request.TrabalhadorId)
                .Select(a => new AsoResumo(a.Id, a.TrabalhadorId, a.DataExame, a.DataValidade, a.ResultadoStatus))
                .ToListAsync(ct)));

        if (aso is null)
        {
            return new EligibilityCheckItem
            {
                Requisito = NomeRequisito,
                Atendido = false,
                Critico = true,
                Detalhe = "Trabalhador não possui ASO cadastrado."
            };
        }

        var situacao = AsoVigente.Classificar(aso, DateTime.UtcNow.Date);
        var atendido = situacao == SituacaoAso.Liberado;

        string? detalhe = situacao switch
        {
            SituacaoAso.Liberado => null,
            SituacaoAso.Vencido => $"ASO vencido em {aso.Value.DataValidade:dd/MM/yyyy}.",
            SituacaoAso.SemParecer => "ASO sem parecer do médico.",
            _ => $"ASO com status '{aso.Value.Resultado}'.",
        };

        return new EligibilityCheckItem
        {
            Requisito = NomeRequisito,
            Atendido = atendido,
            Critico = true,
            Detalhe = detalhe
        };
    }
}
