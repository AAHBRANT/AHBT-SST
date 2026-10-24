using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

// LGPD (12/10): funcionário desligado (G-RH) ou excluído não pode continuar com a biometria ativa. Remove a
// pessoa do reconhecimento facial no Azure e arquiva (Ativo = false) as fotos de cadastro facial e as digitais
// cadastradas — o mesmo arquivamento do "Refazer cadastro facial", que preserva a foto usada como prova nas
// assinaturas antigas (o log de assinaturas a busca com IgnoreQueryFilters). As assinaturas já feitas
// continuam válidas; se a pessoa for recontratada, faz cadastro novo.
// Melhor esforço: o desligamento já está gravado e não pode falhar por causa do Azure (fica no log).
internal static class RemocaoBiometriaDesligamento
{
    public static async Task TentarAsync(IAppDbContext db, IAutenticacaoFacialService? facial, ILogger logger, Guid trabalhadorId, CancellationToken ct)
    {
        try
        {
            var trabalhador = await db.Trabalhadores.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == trabalhadorId, ct);
            if (trabalhador is null)
                return;

            if (trabalhador.AzureFacePersonId is not null && facial is not null)
            {
                await facial.RemoverCadastroAsync(trabalhadorId, ct);
                trabalhador.AzureFacePersonId = null;
            }

            db.FotosCadastroFacial.RemoveRange(await db.FotosCadastroFacial.IgnoreQueryFilters()
                .Where(f => f.TrabalhadorId == trabalhadorId && f.Ativo).ToListAsync(ct));
            db.TemplatesBiometricoFutronic.RemoveRange(await db.TemplatesBiometricoFutronic.IgnoreQueryFilters()
                .Where(t => t.TrabalhadorId == trabalhadorId && t.Ativo).ToListAsync(ct));
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Não foi possível remover a biometria do trabalhador desligado {TrabalhadorId}.", trabalhadorId);
        }
    }
}
