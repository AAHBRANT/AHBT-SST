using AAHBRANT.SST.Application.MateriaisApoio.Commands;
using AAHBRANT.SST.Application.MateriaisApoio.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.MateriaisApoio;

public class MateriaisApoioQueriesTests
{
    private static byte[] ConteudoJpegFalso() => new byte[] { 0xFF, 0xD8, 0xFF, 0x01, 0x02, 0x03 };

    [Fact]
    public async Task ListarMateriaisApoioQuery_ComFiltroDeCategoria_RetornaSoOsDaquelaCategoria()
    {
        var db = DbContextFactory.Criar();
        var criar = new CriarMaterialApoioCommandHandler(db);
        await criar.Handle(new CriarMaterialApoioCommand("A", "Sinalização - Alojamento", "a.jpeg", "image/jpeg", ConteudoJpegFalso()), default);
        await criar.Handle(new CriarMaterialApoioCommand("B", "Instruções Técnicas", "b.jpeg", "image/jpeg", ConteudoJpegFalso()), default);

        var handler = new ListarMateriaisApoioQueryHandler(db);
        var resultado = await handler.Handle(new ListarMateriaisApoioQuery("Sinalização - Alojamento"), default);

        var item = Assert.Single(resultado);
        Assert.Equal("A", item.Nome);
    }

    [Fact]
    public async Task ListarMateriaisApoioQuery_SemFiltro_RetornaTodosComTamanhoCalculado()
    {
        var db = DbContextFactory.Criar();
        var criar = new CriarMaterialApoioCommandHandler(db);
        await criar.Handle(new CriarMaterialApoioCommand("A", null, "a.jpeg", "image/jpeg", ConteudoJpegFalso()), default);

        var handler = new ListarMateriaisApoioQueryHandler(db);
        var resultado = await handler.Handle(new ListarMateriaisApoioQuery(), default);

        var item = Assert.Single(resultado);
        Assert.Equal(ConteudoJpegFalso().Length, item.TamanhoBytes);
    }

    [Fact]
    public async Task ObterConteudoMaterialApoioQuery_MaterialExistente_RetornaBytesEContentType()
    {
        var db = DbContextFactory.Criar();
        var criar = new CriarMaterialApoioCommandHandler(db);
        var id = await criar.Handle(new CriarMaterialApoioCommand("A", null, "a.jpeg", "image/jpeg", ConteudoJpegFalso()), default);

        var handler = new ObterConteudoMaterialApoioQueryHandler(db);
        var resultado = await handler.Handle(new ObterConteudoMaterialApoioQuery(id), default);

        Assert.NotNull(resultado);
        Assert.Equal(ConteudoJpegFalso(), resultado!.Conteudo);
        Assert.Equal("image/jpeg", resultado.ContentType);
        Assert.Equal("a.jpeg", resultado.NomeArquivo);
    }

    [Fact]
    public async Task ObterConteudoMaterialApoioQuery_MaterialInexistente_RetornaNulo()
    {
        var db = DbContextFactory.Criar();
        var handler = new ObterConteudoMaterialApoioQueryHandler(db);

        var resultado = await handler.Handle(new ObterConteudoMaterialApoioQuery(Guid.NewGuid()), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task ExcluirMaterialApoioCommand_MaterialExistente_SomeDaListagem()
    {
        var db = DbContextFactory.Criar();
        var criar = new CriarMaterialApoioCommandHandler(db);
        var id = await criar.Handle(new CriarMaterialApoioCommand("A", null, "a.jpeg", "image/jpeg", ConteudoJpegFalso()), default);

        var excluir = new ExcluirMaterialApoioCommandHandler(db);
        await excluir.Handle(new ExcluirMaterialApoioCommand(id), default);

        var listar = new ListarMateriaisApoioQueryHandler(db);
        var resultado = await listar.Handle(new ListarMateriaisApoioQuery(), default);
        Assert.Empty(resultado);

        // Soft delete (Ativo=false), não hard delete — mesmo padrão global do SstDbContext.
        var materialAindaExiste = await db.MateriaisApoio.IgnoreQueryFilters().AnyAsync(m => m.Id == id);
        Assert.True(materialAindaExiste);
    }
}
