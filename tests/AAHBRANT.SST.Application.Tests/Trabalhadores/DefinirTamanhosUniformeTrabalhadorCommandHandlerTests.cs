using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Trabalhadores;

public class DefinirTamanhosUniformeTrabalhadorCommandHandlerTests
{
    private static async Task<(Trabalhador Trabalhador, CatalogoUniforme Camisa, CatalogoUniforme Calca)> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Nome = "Obra Teste" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var camisa = new CatalogoUniforme { Nome = "Camisa" };
        var calca = new CatalogoUniforme { Nome = "Calça" };
        var trabalhador = new Trabalhador
        {
            Nome = "Bruno Silva Santos",
            Matricula = "00427",
            Cpf = "12345678900",
            Obra = obra,
            Funcao = funcao,
            DataAdmissao = DateTime.UtcNow,
        };

        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.CatalogoUniformes.AddRange(camisa, calca);
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        return (trabalhador, camisa, calca);
    }

    [Fact]
    public async Task Handle_TrabalhadorSemTamanhos_AdicionaTodosOsItensInformados()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, camisa, calca) = await SemearAsync(db);
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db);

        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme>
        {
            new(camisa.Id, "M"),
            new(calca.Id, "42"),
        }), default);

        var vinculos = await db.TrabalhadorTamanhosUniforme.Where(t => t.TrabalhadorId == trabalhador.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.Equal("M", vinculos.Single(v => v.CatalogoUniformeId == camisa.Id).Tamanho);
        Assert.Equal("42", vinculos.Single(v => v.CatalogoUniformeId == calca.Id).Tamanho);
    }

    [Fact]
    public async Task Handle_ReenviaMesmaPecaComTamanhoDiferente_AtualizaOTamanhoDoVinculoExistente()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, camisa, _) = await SemearAsync(db);
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db);
        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme> { new(camisa.Id, "M") }), default);

        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme> { new(camisa.Id, "G") }), default);

        var vinculos = await db.TrabalhadorTamanhosUniforme.Where(t => t.TrabalhadorId == trabalhador.Id).ToListAsync();
        Assert.Single(vinculos);
        Assert.Equal("G", vinculos[0].Tamanho);
    }

    [Fact]
    public async Task Handle_RemovePecaDaLista_DesativaVinculoExistente()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, camisa, calca) = await SemearAsync(db);
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db);
        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme>
        {
            new(camisa.Id, "M"),
            new(calca.Id, "42"),
        }), default);

        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme> { new(camisa.Id, "M") }), default);

        var vinculos = await db.TrabalhadorTamanhosUniforme.IgnoreQueryFilters().Where(t => t.TrabalhadorId == trabalhador.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.True(vinculos.Single(v => v.CatalogoUniformeId == camisa.Id).Ativo);
        Assert.False(vinculos.Single(v => v.CatalogoUniformeId == calca.Id).Ativo);
    }

    [Fact]
    public async Task Handle_TrabalhadorInexistente_LancaKeyNotFoundException()
    {
        var db = DbContextFactory.Criar();
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(Guid.NewGuid(), new List<ItemTamanhoUniforme>()), default));
    }
}
