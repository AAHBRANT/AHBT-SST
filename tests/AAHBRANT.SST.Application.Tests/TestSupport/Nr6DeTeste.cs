using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.TestSupport;

// A entrega de EPI só é registrada com treinamento de NR-06 válido (trava no servidor, auditoria de
// 06/10/2026). Testes que precisam de uma entrega, e não testam essa trava, semeiam o certificado aqui.
public static class Nr6DeTeste
{
    public static async Task SemearAsync(IAppDbContext db, Guid trabalhadorId)
    {
        var curso = await db.CursosTreinamento.FirstOrDefaultAsync(c => c.AtendeNr6)
            ?? db.CursosTreinamento.Add(new CursoTreinamento { Nome = "NR-06 Uso de EPI", NormaReferencia = "NR-06", AtendeNr6 = true }).Entity;
        db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = trabalhadorId,
            CursoTreinamentoId = curso.Id,
            DataRealizacao = DateTime.UtcNow.AddMonths(-1),
            DataValidade = DateTime.UtcNow.AddYears(1),
            NumeroCertificado = "LP-TESTE",
        });
        await db.SaveChangesAsync();
    }
}
