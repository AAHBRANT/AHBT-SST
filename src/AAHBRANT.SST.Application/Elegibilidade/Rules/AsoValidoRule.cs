using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Elegibilidade.Rules;

// §45 da Base de Conhecimento — checa o ASO mais recente do trabalhador: precisa estar
// com ResultadoStatus=Apto (ou AptoComRestricao) e dentro da validade.
public class AsoValidoRule : IEligibilityRule
{
    private readonly IAppDbContext _db;

    public string NomeRequisito => "ASO válido";

    public AsoValidoRule(IAppDbContext db) => _db = db;

    public async Task<EligibilityCheckItem> AvaliarAsync(EligibilityRequest request, CancellationToken ct = default)
    {
        var aso = await _db.Asos
            .Where(a => a.TrabalhadorId == request.TrabalhadorId)
            .OrderByDescending(a => a.DataExame)
            .FirstOrDefaultAsync(ct);

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

        var statusOk = aso.ResultadoStatus is ResultadoAso.Apto or ResultadoAso.AptoComRestricao;
        var dentroValidade = aso.DataValidade.Date >= DateTime.UtcNow.Date;
        var atendido = statusOk && dentroValidade;

        string? detalhe = atendido
            ? null
            : !dentroValidade
                ? $"ASO vencido em {aso.DataValidade:dd/MM/yyyy}."
                : $"ASO com status '{aso.ResultadoStatus}'.";

        return new EligibilityCheckItem
        {
            Requisito = NomeRequisito,
            Atendido = atendido,
            Critico = true,
            Detalhe = detalhe
        };
    }
}
