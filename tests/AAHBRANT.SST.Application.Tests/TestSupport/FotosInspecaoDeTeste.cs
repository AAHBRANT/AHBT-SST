using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.TestSupport;

// Desde 18/11/2026 toda inspeção exige 2 fotos em cada item do checklist para encerrar. Testes que não tratam de
// foto usam isto para deixar a inspeção pronta: foto 1 na resposta e foto 2 em InspecaoItemFotoAdicional.
public static class FotosInspecaoDeTeste
{
    public static async Task CompletarAsync(IAppDbContext db, Guid inspecaoId)
    {
        var respostas = await db.InspecaoItemRespostas.Where(r => r.InspecaoId == inspecaoId && r.Ativo).ToListAsync();
        foreach (var r in respostas)
        {
            if (r.FotoConteudo.Length == 0)
            {
                r.FotoConteudo = new byte[] { 1, 2, 3 };
                r.FotoContentType = "image/jpeg";
            }
            db.InspecaoItemFotosAdicionais.Add(new InspecaoItemFotoAdicional
            {
                InspecaoItemRespostaId = r.Id,
                Ordem = 2,
                FotoConteudo = new byte[] { 4, 5, 6 },
                FotoContentType = "image/jpeg",
            });
        }
        await db.SaveChangesAsync();
    }
}
