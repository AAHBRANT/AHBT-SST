using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pgrs.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Pgrs;

public class AnexarDocumentoPgrCommandHandlerTests
{
    private static readonly byte[] PdfMinimo = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_PgrExistente_GravaConteudoEContentType()
    {
        var db = CriarDb(nameof(Handle_PgrExistente_GravaConteudoEContentType));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Central", Cliente = "Consórcio Exemplo", Cnpj = "12.345.678/0001-90" };
        var pgr = new Pgr { ObraId = obra.Id, Obra = obra, Nome = "PGR Teste", DataElaboracao = DateTime.UtcNow };
        db.Obras.Add(obra);
        db.Pgrs.Add(pgr);
        await db.SaveChangesAsync(default);

        var handler = new AnexarDocumentoPgrCommandHandler(db);
        await handler.Handle(new AnexarDocumentoPgrCommand(pgr.Id, PdfMinimo, "application/pdf"), default);

        var atualizado = await db.Pgrs.FirstAsync(p => p.Id == pgr.Id);
        Assert.Equal(PdfMinimo, atualizado.DocumentoConteudo);
        Assert.Equal("application/pdf", atualizado.DocumentoContentType);
    }

    [Fact]
    public async Task Handle_PgrInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_PgrInexistente_LancaKeyNotFoundException));
        var handler = new AnexarDocumentoPgrCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new AnexarDocumentoPgrCommand(Guid.NewGuid(), PdfMinimo, "application/pdf"), default));
    }
}
