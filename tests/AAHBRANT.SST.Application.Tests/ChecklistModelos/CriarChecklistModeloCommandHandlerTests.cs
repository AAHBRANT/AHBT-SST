using AAHBRANT.SST.Application.ChecklistModelos.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.ChecklistModelos;

public class CriarChecklistModeloCommandHandlerTests
{
    [Fact]
    public async Task Handle_ItemComSecaoPreenchida_PersisteSecaoNoItem()
    {
        var db = DbContextFactory.Criar();
        var handler = new CriarChecklistModeloCommandHandler(db);
        var command = new CriarChecklistModeloCommand(
            "Checklist de Alojamento",
            TipoInspecao.Alojamento,
            new List<CriarChecklistModeloItemInput>
            {
                new("Piso resistente, lavável e impermeável", false, false, false, "Estrutura mínima do alojamento"),
                new("Cama individual para cada trabalhador", false, false, false, "Dormitórios"),
            });

        var id = await handler.Handle(command, default);

        var itens = await db.ChecklistModeloItens.Where(i => i.ChecklistModeloId == id).OrderBy(i => i.Ordem).ToListAsync();
        Assert.Equal(2, itens.Count);
        Assert.Equal("Estrutura mínima do alojamento", itens[0].Secao);
        Assert.Equal("Dormitórios", itens[1].Secao);
    }

    [Fact]
    public async Task Handle_ItemSemSecao_PersisteSecaoNula()
    {
        var db = DbContextFactory.Criar();
        var handler = new CriarChecklistModeloCommandHandler(db);
        var command = new CriarChecklistModeloCommand(
            "Checklist de Obra",
            TipoInspecao.Obra,
            new List<CriarChecklistModeloItemInput>
            {
                new("Sinalização de segurança visível", false, false, false),
            });

        var id = await handler.Handle(command, default);

        var item = await db.ChecklistModeloItens.SingleAsync(i => i.ChecklistModeloId == id);
        Assert.Null(item.Secao);
    }
}
