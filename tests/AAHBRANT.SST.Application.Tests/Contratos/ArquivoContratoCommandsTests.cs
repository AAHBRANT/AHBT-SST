using System.Text;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Contratos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Contratos;

public class ArquivoContratoCommandsTests
{
    private static readonly byte[] PdfMinimo = Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF");

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<Contrato> SemearContratoAsync(IAppDbContext db)
    {
        var contrato = new Contrato
        {
            Empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" },
            Obra = new Obra { Codigo = "O1", Nome = "Obra 1" },
            NumeroContrato = "CT-1",
            DataInicioVigencia = new DateOnly(2026, 1, 1),
            DataFimVigencia = new DateOnly(2026, 12, 31),
        };
        db.Contratos.Add(contrato);
        await db.SaveChangesAsync();
        return contrato;
    }

    [Fact]
    public async Task Anexar_GuardaPdfEListagemPassaATerArquivo()
    {
        var db = CriarDb(nameof(Anexar_GuardaPdfEListagemPassaATerArquivo));
        var contrato = await SemearContratoAsync(db);

        await new AnexarArquivoContratoCommandHandler(db).Handle(
            new AnexarArquivoContratoCommand(contrato.Id, "contrato.pdf", "application/octet-stream", PdfMinimo), default);

        var obtido = await new ObterArquivoContratoQueryHandler(db).Handle(new ObterArquivoContratoQuery(contrato.Id), default);
        Assert.NotNull(obtido);
        Assert.Equal("contrato.pdf", obtido!.NomeArquivo);
        Assert.Equal("application/pdf", obtido.ContentType);
        Assert.Equal(PdfMinimo, obtido.Conteudo);
    }

    [Fact]
    public async Task Anexar_DeNovo_SubstituiOArquivoAnterior()
    {
        var db = CriarDb(nameof(Anexar_DeNovo_SubstituiOArquivoAnterior));
        var contrato = await SemearContratoAsync(db);
        var handler = new AnexarArquivoContratoCommandHandler(db);
        await handler.Handle(new AnexarArquivoContratoCommand(contrato.Id, "v1.pdf", "application/pdf", PdfMinimo), default);

        await handler.Handle(new AnexarArquivoContratoCommand(contrato.Id, "v2.pdf", "application/pdf", PdfMinimo), default);

        var arquivo = await db.ArquivosContrato.SingleAsync();
        Assert.Equal("v2.pdf", arquivo.NomeArquivo);
    }

    [Fact]
    public async Task Anexar_ContratoInexistente_LancaKeyNotFound()
    {
        var db = CriarDb(nameof(Anexar_ContratoInexistente_LancaKeyNotFound));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new AnexarArquivoContratoCommandHandler(db).Handle(
            new AnexarArquivoContratoCommand(Guid.NewGuid(), "c.pdf", "application/pdf", PdfMinimo), default));
    }

    [Fact]
    public async Task Obter_SemArquivo_RetornaNulo()
    {
        var db = CriarDb(nameof(Obter_SemArquivo_RetornaNulo));
        var contrato = await SemearContratoAsync(db);

        Assert.Null(await new ObterArquivoContratoQueryHandler(db).Handle(new ObterArquivoContratoQuery(contrato.Id), default));
    }

    [Fact]
    public void Validador_RecusaArquivoQueNaoEPdf()
    {
        var resultado = new AnexarArquivoContratoCommandValidator().Validate(
            new AnexarArquivoContratoCommand(Guid.NewGuid(), "foto.pdf", "application/pdf", Encoding.ASCII.GetBytes("GIF89a")));

        Assert.Contains(resultado.Errors, e => e.ErrorMessage == "Envie o contrato em PDF.");
    }

    [Fact]
    public void Validador_AceitaPdf()
    {
        var resultado = new AnexarArquivoContratoCommandValidator().Validate(
            new AnexarArquivoContratoCommand(Guid.NewGuid(), "contrato.pdf", "application/pdf", PdfMinimo));

        Assert.True(resultado.IsValid);
    }
}
