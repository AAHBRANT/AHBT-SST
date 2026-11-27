using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Trabalhadores;

// Auditoria 12/11/2026: FotoCadastroFacial não tem filtro por obra, então listar e baixar a foto
// facial precisam conferir o escopo do trabalhador — senão a biometria de uma obra saía para quem
// só tem acesso a outra.
public class FotosCadastroFacialEscopoPorObraTests
{
    static FotosCadastroFacialEscopoPorObraTests() => ChavesCpfDeTeste.Configurar();

    private static readonly Guid ObraA = Guid.NewGuid();
    private static readonly Guid ObraB = Guid.NewGuid();

    // Mesma base InMemory, dois contextos: um global (para semear) e um restrito à obra A.
    private static (SstDbContext global, SstDbContext restrito) CriarContextos()
    {
        var nome = Guid.NewGuid().ToString();
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options;
        var global = new SstDbContext(opcoes, new CurrentUserService());

        var usuarioRestrito = new CurrentUserService();
        usuarioRestrito.DefinirEscopo(false, new[] { ObraA });
        var restrito = new SstDbContext(opcoes, usuarioRestrito);
        return (global, restrito);
    }

    private static async Task<(Trabalhador deA, FotoCadastroFacial fotoA, Trabalhador deB, FotoCadastroFacial fotoB)> SemearAsync(SstDbContext db)
    {
        var funcao = new Funcao { Nome = "Pedreiro" };
        var deA = new Trabalhador { ObraId = ObraA, Funcao = funcao, Nome = "Trabalhador A", Cpf = "00000000001" };
        var deB = new Trabalhador { ObraId = ObraB, Funcao = funcao, Nome = "Trabalhador B", Cpf = "00000000002" };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.AddRange(deA, deB);
        await db.SaveChangesAsync();

        var fotoA = NovaFoto(deA.Id);
        var fotoB = NovaFoto(deB.Id);
        db.FotosCadastroFacial.AddRange(fotoA, fotoB);
        await db.SaveChangesAsync();
        return (deA, fotoA, deB, fotoB);
    }

    private static FotoCadastroFacial NovaFoto(Guid trabalhadorId) => new()
    {
        TrabalhadorId = trabalhadorId,
        Conteudo = new byte[] { 1, 2, 3 },
        ContentType = "image/jpeg",
        HashSha256 = "h",
        CapturadaEm = DateTime.UtcNow,
    };

    [Fact]
    public async Task Listar_TrabalhadorDeOutraObra_Bloqueia()
    {
        var (global, restrito) = CriarContextos();
        var (_, _, deB, _) = await SemearAsync(global);
        var handler = new ListarFotosCadastroFacialQueryHandler(restrito);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new ListarFotosCadastroFacialQuery(deB.Id), default));
    }

    [Fact]
    public async Task Obter_FotoDeTrabalhadorDeOutraObra_Bloqueia()
    {
        var (global, restrito) = CriarContextos();
        var (_, _, deB, fotoB) = await SemearAsync(global);
        var handler = new ObterFotoCadastroFacialQueryHandler(restrito);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new ObterFotoCadastroFacialQuery(deB.Id, fotoB.Id), default));
    }

    [Fact]
    public async Task ListarEObter_TrabalhadorDaPropriaObra_Funciona()
    {
        var (global, restrito) = CriarContextos();
        var (deA, fotoA, _, _) = await SemearAsync(global);

        var lista = await new ListarFotosCadastroFacialQueryHandler(restrito)
            .Handle(new ListarFotosCadastroFacialQuery(deA.Id), default);
        var foto = await new ObterFotoCadastroFacialQueryHandler(restrito)
            .Handle(new ObterFotoCadastroFacialQuery(deA.Id, fotoA.Id), default);

        Assert.Equal(fotoA.Id, Assert.Single(lista).Id);
        Assert.NotNull(foto);
        Assert.Equal(new byte[] { 1, 2, 3 }, foto!.Conteudo);
    }

    [Fact]
    public async Task Listar_AcessoGlobal_VeFotosDeQualquerObra()
    {
        var (global, _) = CriarContextos();
        var (_, _, deB, fotoB) = await SemearAsync(global);

        var lista = await new ListarFotosCadastroFacialQueryHandler(global)
            .Handle(new ListarFotosCadastroFacialQuery(deB.Id), default);

        Assert.Equal(fotoB.Id, Assert.Single(lista).Id);
    }
}
