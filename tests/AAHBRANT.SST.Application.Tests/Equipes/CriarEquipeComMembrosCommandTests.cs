using AAHBRANT.SST.Application.Equipes.Commands;
using AAHBRANT.SST.Application.Equipes.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Equipes;

// Equipe montada dentro da APR (24/09/2026): salva a seleção de responsáveis como equipe da obra.
public class CriarEquipeComMembrosCommandTests
{
    [Fact]
    public async Task Handle_CriaEquipeNoSetorGeralEVinculaMembros_ListaDevolveMembros()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Ponte", Codigo = "PON" };
        db.Obras.Add(obra);
        var a = new Trabalhador { Nome = "Ana", ObraId = obra.Id };
        var b = new Trabalhador { Nome = "Bruno", ObraId = obra.Id };
        var fora = new Trabalhador { Nome = "Carlos", ObraId = obra.Id };
        db.Trabalhadores.AddRange(a, b, fora);
        await db.SaveChangesAsync();

        var id = await new CriarEquipeComMembrosCommandHandler(db)
            .Handle(new CriarEquipeComMembrosCommand(obra.Id, "  Armação  ", a.Id, new() { a.Id, b.Id }), default);

        var equipes = await new ListarEquipesQueryHandler(db).Handle(new ListarEquipesQuery(obra.Id, null), default);
        var equipe = Assert.Single(equipes);
        Assert.Equal(id, equipe.Id);
        Assert.Equal("Armação", equipe.Nome);
        Assert.Equal("Geral", equipe.SetorNome);
        Assert.Equal(a.Id, equipe.EncarregadoId);
        Assert.Equal(new[] { a.Id, b.Id }.OrderBy(x => x), equipe.TrabalhadorIds.OrderBy(x => x));
    }

    [Fact]
    public async Task Handle_SegundaEquipe_ReusaSetorGeralEMoveMembroDeEquipe()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Ponte", Codigo = "PON" };
        db.Obras.Add(obra);
        var a = new Trabalhador { Nome = "Ana", ObraId = obra.Id };
        db.Trabalhadores.Add(a);
        await db.SaveChangesAsync();
        var handler = new CriarEquipeComMembrosCommandHandler(db);

        await handler.Handle(new CriarEquipeComMembrosCommand(obra.Id, "Equipe 1", null, new() { a.Id }), default);
        var segunda = await handler.Handle(new CriarEquipeComMembrosCommand(obra.Id, "Equipe 2", null, new() { a.Id }), default);

        Assert.Equal(1, await db.Setores.CountAsync());
        Assert.Equal(segunda, (await db.Trabalhadores.SingleAsync()).EquipeId);
    }

    [Fact]
    public async Task Handle_NomeRepetidoNaObra_Recusa()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Ponte", Codigo = "PON" };
        db.Obras.Add(obra);
        var a = new Trabalhador { Nome = "Ana", ObraId = obra.Id };
        db.Trabalhadores.Add(a);
        await db.SaveChangesAsync();
        var handler = new CriarEquipeComMembrosCommandHandler(db);
        await handler.Handle(new CriarEquipeComMembrosCommand(obra.Id, "Armação", null, new() { a.Id }), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CriarEquipeComMembrosCommand(obra.Id, "Armação", null, new() { a.Id }), default));
    }

    [Fact]
    public async Task Handle_EncarregadoForaDosMembros_Recusa()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Ponte", Codigo = "PON" };
        db.Obras.Add(obra);
        var a = new Trabalhador { Nome = "Ana", ObraId = obra.Id };
        var b = new Trabalhador { Nome = "Bruno", ObraId = obra.Id };
        db.Trabalhadores.AddRange(a, b);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CriarEquipeComMembrosCommandHandler(db)
                .Handle(new CriarEquipeComMembrosCommand(obra.Id, "Armação", b.Id, new() { a.Id }), default));
    }
}
