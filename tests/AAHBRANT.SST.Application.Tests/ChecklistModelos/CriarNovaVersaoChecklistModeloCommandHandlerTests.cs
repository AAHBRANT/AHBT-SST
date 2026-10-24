using AAHBRANT.SST.Application.ChecklistModelos.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.ChecklistModelos;

public class CriarNovaVersaoChecklistModeloCommandHandlerTests
{
    [Fact]
    public async Task Handle_NovaVersaoComSecao_PreservaSecaoDosItens()
    {
        var db = DbContextFactory.Criar();
        var anterior = new ChecklistModelo { Nome = "Checklist de Alojamento", TipoInspecao = TipoInspecao.Alojamento, Versao = 1 };
        db.ChecklistModelos.Add(anterior);
        await db.SaveChangesAsync();

        var handler = new CriarNovaVersaoChecklistModeloCommandHandler(db);
        var novaVersaoId = await handler.Handle(
            new CriarNovaVersaoChecklistModeloCommand(
                anterior.Id,
                new List<CriarChecklistModeloItemInput>
                {
                    new("Piso resistente, lavável e impermeável", false, false, false, "Estrutura mínima do alojamento"),
                }),
            default);

        var item = await db.ChecklistModeloItens.SingleAsync(i => i.ChecklistModeloId == novaVersaoId);
        Assert.Equal("Estrutura mínima do alojamento", item.Secao);
    }
}
