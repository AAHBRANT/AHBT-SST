using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pcmsos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Pcmsos;

public class ObterDocumentoPcmsoQueryHandlerTests
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
    public async Task Handle_PcmsoSemDocumento_RetornaNull()
    {
        var db = CriarDb(nameof(Handle_PcmsoSemDocumento_RetornaNull));
        var pcmso = new PcmsoDetalhe { Nome = "PCMSO Teste", DataEmissao = DateTime.UtcNow };
        db.PcmsoDetalhes.Add(pcmso);
        await db.SaveChangesAsync(default);

        var handler = new ObterDocumentoPcmsoQueryHandler(db);
        var resultado = await handler.Handle(new ObterDocumentoPcmsoQuery(pcmso.Id), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Handle_PcmsoComDocumento_RetornaConteudoEContentType()
    {
        var db = CriarDb(nameof(Handle_PcmsoComDocumento_RetornaConteudoEContentType));
        var pcmso = new PcmsoDetalhe
        {
            Nome = "PCMSO Teste",
            DataEmissao = DateTime.UtcNow,
            DocumentoConteudo = PdfMinimo,
            DocumentoContentType = "application/pdf",
        };
        db.PcmsoDetalhes.Add(pcmso);
        await db.SaveChangesAsync(default);

        var handler = new ObterDocumentoPcmsoQueryHandler(db);
        var resultado = await handler.Handle(new ObterDocumentoPcmsoQuery(pcmso.Id), default);

        Assert.NotNull(resultado);
        Assert.Equal(PdfMinimo, resultado!.Conteudo);
        Assert.Equal("application/pdf", resultado.ContentType);
        Assert.Equal($"pcmso-{pcmso.Id}.pdf", resultado.NomeArquivo);
    }
}
