using AAHBRANT.SST.Application.MateriaisApoio.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.MateriaisApoio;

public class CriarMaterialApoioCommandHandlerTests
{
    // Assinatura JPEG (0xFF 0xD8 0xFF) + padding — suficiente pra passar em
    // ValidadorAssinaturaArquivo sem precisar de um arquivo real.
    private static byte[] ConteudoJpegFalso() => new byte[] { 0xFF, 0xD8, 0xFF, 0x01, 0x02, 0x03 };

    [Fact]
    public async Task Handle_MaterialValido_PersisteComCategoria()
    {
        var db = DbContextFactory.Criar();
        var handler = new CriarMaterialApoioCommandHandler(db);

        var id = await handler.Handle(
            new CriarMaterialApoioCommand("Regras da Casa", "Sinalização - Alojamento", "regras-da-casa.jpeg", "image/jpeg", ConteudoJpegFalso()),
            default);

        var material = await db.MateriaisApoio.SingleAsync(m => m.Id == id);
        Assert.Equal("Regras da Casa", material.Nome);
        Assert.Equal("Sinalização - Alojamento", material.Categoria);
        Assert.Equal("regras-da-casa.jpeg", material.NomeArquivo);
        Assert.Equal("image/jpeg", material.ContentType);
        Assert.Equal(ConteudoJpegFalso(), material.Conteudo);
    }

    [Fact]
    public async Task Handle_SemCategoria_PersisteCategoriaNula()
    {
        var db = DbContextFactory.Criar();
        var handler = new CriarMaterialApoioCommandHandler(db);

        var id = await handler.Handle(
            new CriarMaterialApoioCommand("Material Geral", null, "arquivo.jpeg", "image/jpeg", ConteudoJpegFalso()),
            default);

        var material = await db.MateriaisApoio.SingleAsync(m => m.Id == id);
        Assert.Null(material.Categoria);
    }
}
