using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Pgrs.Queries;
using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Pgrs;

public class ObterDocumentoPgrQueryHandlerTests
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
    public async Task Handle_PgrSemDocumento_RetornaNull()
    {
        var db = CriarDb(nameof(Handle_PgrSemDocumento_RetornaNull));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Central", Cliente = "Consórcio Exemplo", Cnpj = "12.345.678/0001-90" };
        var pgr = new Pgr { ObraId = obra.Id, Obra = obra, Nome = "PGR Teste", DataElaboracao = DateTime.UtcNow };
        db.Obras.Add(obra);
        db.Pgrs.Add(pgr);
        await db.SaveChangesAsync(default);

        var handler = new ObterDocumentoPgrQueryHandler(db);
        var resultado = await handler.Handle(new ObterDocumentoPgrQuery(pgr.Id), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Handle_PgrComDocumento_RetornaConteudoEContentType()
    {
        var db = CriarDb(nameof(Handle_PgrComDocumento_RetornaConteudoEContentType));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Central", Cliente = "Consórcio Exemplo", Cnpj = "12.345.678/0001-90" };
        var pgr = new Pgr
        {
            ObraId = obra.Id,
            Obra = obra,
            Nome = "PGR Teste",
            DataElaboracao = DateTime.UtcNow,
            DocumentoConteudo = PdfMinimo,
            DocumentoContentType = "application/pdf",
        };
        db.Obras.Add(obra);
        db.Pgrs.Add(pgr);
        await db.SaveChangesAsync(default);

        var handler = new ObterDocumentoPgrQueryHandler(db);
        var resultado = await handler.Handle(new ObterDocumentoPgrQuery(pgr.Id), default);

        Assert.NotNull(resultado);
        Assert.Equal(PdfMinimo, resultado!.Conteudo);
        Assert.Equal("application/pdf", resultado.ContentType);
        Assert.Equal($"pgr-{pgr.Id}.pdf", resultado.NomeArquivo);
    }

    // Caso Beira Rio (05/11/2026): PGR aprovado pelo gerador ficava sem documento na aba PGR.
    [Fact]
    public async Task Handle_PgrAprovadoPeloGerador_RetornaPdfAssinadoCongelado()
    {
        var db = CriarDb(nameof(Handle_PgrAprovadoPeloGerador_RetornaPdfAssinadoCongelado));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Central", Cliente = "Consórcio Exemplo", Cnpj = "12.345.678/0001-90" };
        var pgr = new Pgr { ObraId = obra.Id, Obra = obra, Nome = "PGR Teste", DataElaboracao = DateTime.UtcNow };
        var geracao = new GeracaoPgrEap { ObraId = obra.Id, Nome = "PGR Teste", PgrId = pgr.Id, Status = StatusGeracaoPgrEap.Aprovado, AprovadoEmUtc = DateTime.UtcNow };
        db.Obras.Add(obra);
        db.Pgrs.Add(pgr);
        db.GeracoesPgrEap.Add(geracao);
        db.DocumentosAssinatura.Add(new DocumentoAssinatura
        {
            EntidadeTipo = AssinaturaGeracaoPgr.EntidadeTipo, EntidadeId = geracao.Id,
            Status = StatusDocumentoAssinatura.Finalizado, PdfConteudo = PdfMinimo,
        });
        await db.SaveChangesAsync(default);

        var resultado = await new ObterDocumentoPgrQueryHandler(db).Handle(new ObterDocumentoPgrQuery(pgr.Id), default);

        Assert.NotNull(resultado);
        Assert.Equal(PdfMinimo, resultado!.Conteudo);
        Assert.Equal("application/pdf", resultado.ContentType);
    }

    [Fact]
    public async Task Handle_DocumentoAnexadoTemPrioridadeSobreGeracao()
    {
        var db = CriarDb(nameof(Handle_DocumentoAnexadoTemPrioridadeSobreGeracao));
        var anexado = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x41 };
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Central", Cliente = "Consórcio Exemplo", Cnpj = "12.345.678/0001-90" };
        var pgr = new Pgr { ObraId = obra.Id, Obra = obra, Nome = "PGR Teste", DataElaboracao = DateTime.UtcNow, DocumentoConteudo = anexado, DocumentoContentType = "application/pdf" };
        var geracao = new GeracaoPgrEap { ObraId = obra.Id, Nome = "PGR Teste", PgrId = pgr.Id, Status = StatusGeracaoPgrEap.Aprovado, AprovadoEmUtc = DateTime.UtcNow };
        db.Obras.Add(obra);
        db.Pgrs.Add(pgr);
        db.GeracoesPgrEap.Add(geracao);
        db.DocumentosAssinatura.Add(new DocumentoAssinatura
        {
            EntidadeTipo = AssinaturaGeracaoPgr.EntidadeTipo, EntidadeId = geracao.Id,
            Status = StatusDocumentoAssinatura.Finalizado, PdfConteudo = PdfMinimo,
        });
        await db.SaveChangesAsync(default);

        var resultado = await new ObterDocumentoPgrQueryHandler(db).Handle(new ObterDocumentoPgrQuery(pgr.Id), default);

        Assert.Equal(anexado, resultado!.Conteudo);
    }
}
