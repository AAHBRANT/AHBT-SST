using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pcmsos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Pcmsos;

public class AnexarDocumentoPcmsoCommandHandlerTests
{
    private static readonly byte[] PdfMinimo = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_PcmsoExistente_GravaConteudoEContentType()
    {
        var db = CriarDb(nameof(Handle_PcmsoExistente_GravaConteudoEContentType));
        var pcmso = new PcmsoDetalhe { Nome = "PCMSO Teste", DataEmissao = DateTime.UtcNow };
        db.PcmsoDetalhes.Add(pcmso);
        await db.SaveChangesAsync(default);

        var handler = new AnexarDocumentoPcmsoCommandHandler(db);
        await handler.Handle(new AnexarDocumentoPcmsoCommand(pcmso.Id, PdfMinimo, "application/pdf"), default);

        var atualizado = await db.PcmsoDetalhes.FirstAsync(p => p.Id == pcmso.Id);
        Assert.Equal(PdfMinimo, atualizado.DocumentoConteudo);
        Assert.Equal("application/pdf", atualizado.DocumentoContentType);
    }

    [Fact]
    public async Task Handle_PcmsoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_PcmsoInexistente_LancaKeyNotFoundException));
        var handler = new AnexarDocumentoPcmsoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new AnexarDocumentoPcmsoCommand(Guid.NewGuid(), PdfMinimo, "application/pdf"), default));
    }
}
