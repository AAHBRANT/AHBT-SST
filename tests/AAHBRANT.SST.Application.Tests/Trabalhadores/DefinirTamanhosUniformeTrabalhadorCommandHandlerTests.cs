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
            TamanhoBlusaEpi = "M",
            TamanhoCalcaEpi = "42",
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
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db, new AAHBRANT.SST.Infrastructure.Seguranca.CurrentUserService());

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
    public async Task Handle_IgnoraTamanhoEnviadoPeloCliente_EUsaCampoSincronizadoDoGrh()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, camisa, _) = await SemearAsync(db);
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db, new AAHBRANT.SST.Infrastructure.Seguranca.CurrentUserService());
        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme> { new(camisa.Id, "G") }), default);

        trabalhador.TamanhoBlusaEpi = "G";
        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme> { new(camisa.Id, "P") }), default);

        // A calça também é sincronizada do G-RH (semeada com "42"); aqui interessa só o vínculo da camisa.
        var vinculos = await db.TrabalhadorTamanhosUniforme
            .Where(t => t.TrabalhadorId == trabalhador.Id && t.CatalogoUniformeId == camisa.Id).ToListAsync();
        Assert.Single(vinculos);
        Assert.Equal("G", vinculos[0].Tamanho);
    }

    [Fact]
    public async Task Handle_TamanhoGrhAusente_DesativaVinculoLegadoSemExcluirLinha()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, camisa, calca) = await SemearAsync(db);
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db, new AAHBRANT.SST.Infrastructure.Seguranca.CurrentUserService());
        // Primeira sincronização cria os dois vínculos (camisa "M", calça "42"); depois o G-RH deixa
        // de informar a calça e a segunda sincronização precisa desativar o vínculo, sem excluir a linha.
        await handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id, new List<ItemTamanhoUniforme> { new(camisa.Id, "M") }), default);

        trabalhador.TamanhoCalcaEpi = null;
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
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db, new AAHBRANT.SST.Infrastructure.Seguranca.CurrentUserService());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(Guid.NewGuid(), new List<ItemTamanhoUniforme>()), default));
    }

    [Fact]
    public async Task Handle_TrabalhadorDeObraNaoAutorizada_NaoSincroniza()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, camisa, _) = await SemearAsync(db);
        var usuario = new AAHBRANT.SST.Infrastructure.Seguranca.CurrentUserService();
        usuario.DefinirEscopo(false, new[] { Guid.NewGuid() });
        var handler = new DefinirTamanhosUniformeTrabalhadorCommandHandler(db, usuario);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new DefinirTamanhosUniformeTrabalhadorCommand(trabalhador.Id,
                new List<ItemTamanhoUniforme> { new(camisa.Id, "G") }), default));

        Assert.Empty(await db.TrabalhadorTamanhosUniforme.IgnoreQueryFilters()
            .Where(t => t.TrabalhadorId == trabalhador.Id).ToListAsync());
    }
}
