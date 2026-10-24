using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Treinamentos.Commands;
using AAHBRANT.SST.Application.Treinamentos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Treinamentos;

// Lançamento retroativo de certificados (22/09): as obras já estavam em andamento quando o sistema
// entrou, e o certificado digitalizado (PDF original ou foto do papel) passou a ser o documento
// probatório de treinamento feito por terceiro.
public class AnexarArquivoCertificadoTreinamentoTests
{
    private static readonly byte[] PdfValido = { 0x25, 0x50, 0x44, 0x46, 0x01, 0x02 };
    private static readonly byte[] JpegValido = { 0xFF, 0xD8, 0xFF, 0x01, 0x02, 0x03 };

    private static async Task<Guid> SemearTreinamentoAsync(Infrastructure.Persistencia.SstDbContext db)
    {
        var treinamento = new Treinamento
        {
            TrabalhadorId = Guid.NewGuid(),
            CursoTreinamentoId = Guid.NewGuid(),
            DataRealizacao = new DateTime(2025, 3, 10),
            DataValidade = new DateTime(2027, 3, 10),
            CargaHorariaRealizada = 8,
        };
        db.Treinamentos.Add(treinamento);
        await db.SaveChangesAsync();
        return treinamento.Id;
    }

    [Fact]
    public async Task Handle_PrimeiroAnexo_PersisteArquivo()
    {
        var db = DbContextFactory.Criar();
        var treinamentoId = await SemearTreinamentoAsync(db);
        var handler = new AnexarArquivoCertificadoTreinamentoCommandHandler(db);

        await handler.Handle(
            new AnexarArquivoCertificadoTreinamentoCommand(treinamentoId, "nr35-joao.pdf", PdfValido, "application/pdf"),
            default);

        var arquivo = await db.ArquivosCertificadoTreinamento.SingleAsync(a => a.TreinamentoId == treinamentoId);
        Assert.Equal("nr35-joao.pdf", arquivo.NomeArquivo);
        Assert.Equal("application/pdf", arquivo.ContentType);
        Assert.Equal(PdfValido, arquivo.Conteudo);
        Assert.Equal(PdfValido.LongLength, arquivo.TamanhoBytes);
    }

    // Reenviar é o caminho normal quando a primeira foto do papel sai ilegível — precisa substituir,
    // não acumular um segundo arquivo (a decisão do usuário é um anexo por certificado).
    [Fact]
    public async Task Handle_ReenvioDoMesmoTreinamento_SubstituiArquivoExistente()
    {
        var db = DbContextFactory.Criar();
        var treinamentoId = await SemearTreinamentoAsync(db);
        var handler = new AnexarArquivoCertificadoTreinamentoCommandHandler(db);

        await handler.Handle(
            new AnexarArquivoCertificadoTreinamentoCommand(treinamentoId, "foto-tremida.jpg", JpegValido, "image/jpeg"),
            default);
        await handler.Handle(
            new AnexarArquivoCertificadoTreinamentoCommand(treinamentoId, "certificado.pdf", PdfValido, "application/pdf"),
            default);

        var arquivos = await db.ArquivosCertificadoTreinamento.Where(a => a.TreinamentoId == treinamentoId).ToListAsync();
        Assert.Single(arquivos);
        Assert.Equal("certificado.pdf", arquivos[0].NomeArquivo);
        Assert.Equal(PdfValido, arquivos[0].Conteudo);
    }

    [Fact]
    public async Task Handle_TreinamentoInexistente_LancaKeyNotFound()
    {
        var db = DbContextFactory.Criar();
        var handler = new AnexarArquivoCertificadoTreinamentoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new AnexarArquivoCertificadoTreinamentoCommand(Guid.NewGuid(), "x.pdf", PdfValido, "application/pdf"),
            default));
    }

    [Fact]
    public async Task ObterArquivo_TreinamentoSemAnexo_RetornaNulo()
    {
        var db = DbContextFactory.Criar();
        var treinamentoId = await SemearTreinamentoAsync(db);
        var handler = new ObterArquivoCertificadoTreinamentoQueryHandler(db);

        var resultado = await handler.Handle(new ObterArquivoCertificadoTreinamentoQuery(treinamentoId), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task RemoverArquivo_TreinamentoSemAnexo_LancaKeyNotFound()
    {
        var db = DbContextFactory.Criar();
        var treinamentoId = await SemearTreinamentoAsync(db);
        var handler = new RemoverArquivoCertificadoTreinamentoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new RemoverArquivoCertificadoTreinamentoCommand(treinamentoId), default));
    }
}

public class AnexarArquivoCertificadoTreinamentoValidatorTests
{
    private static readonly byte[] PdfValido = { 0x25, 0x50, 0x44, 0x46, 0x01, 0x02 };
    private static readonly byte[] JpegValido = { 0xFF, 0xD8, 0xFF, 0x01, 0x02, 0x03 };
    private static readonly byte[] PngValido = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 };

    private static AnexarArquivoCertificadoTreinamentoCommandValidator Validator() => new();

    [Fact]
    public void Validate_Pdf_NaoRetornaErros()
    {
        var resultado = Validator().Validate(
            new AnexarArquivoCertificadoTreinamentoCommand(Guid.NewGuid(), "c.pdf", PdfValido, "application/pdf"));
        Assert.True(resultado.IsValid);
    }

    // Foto do certificado impresso é o caso mais comum no lançamento retroativo — os dois formatos
    // que a câmera do celular/WebView do Teams produz precisam passar.
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    public void Validate_Imagem_NaoRetornaErros(string contentType)
    {
        var conteudo = contentType == "image/jpeg" ? JpegValido : PngValido;
        var resultado = Validator().Validate(
            new AnexarArquivoCertificadoTreinamentoCommand(Guid.NewGuid(), "c.img", conteudo, contentType));
        Assert.True(resultado.IsValid);
    }

    [Fact]
    public void Validate_TipoNaoPermitido_RetornaErro()
    {
        var resultado = Validator().Validate(new AnexarArquivoCertificadoTreinamentoCommand(
            Guid.NewGuid(), "c.docx", new byte[] { 0x50, 0x4B, 0x03, 0x04 },
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
        Assert.False(resultado.IsValid);
    }

    // Content-Type é só um rótulo que o cliente manda — um .exe renomeado para .pdf não pode entrar
    // no acervo probatório da empresa.
    [Fact]
    public void Validate_ConteudoNaoCorrespondeAoTipoDeclarado_RetornaErro()
    {
        var resultado = Validator().Validate(new AnexarArquivoCertificadoTreinamentoCommand(
            Guid.NewGuid(), "falso.pdf", new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, "application/pdf"));
        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void Validate_ArquivoAcimaDe10Mb_RetornaErro()
    {
        var grande = new byte[10 * 1024 * 1024 + 1];
        grande[0] = 0x25; grande[1] = 0x50; grande[2] = 0x44; grande[3] = 0x46;
        var resultado = Validator().Validate(
            new AnexarArquivoCertificadoTreinamentoCommand(Guid.NewGuid(), "grande.pdf", grande, "application/pdf"));
        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void Validate_ArquivoVazio_RetornaErro()
    {
        var resultado = Validator().Validate(
            new AnexarArquivoCertificadoTreinamentoCommand(Guid.NewGuid(), "c.pdf", Array.Empty<byte>(), "application/pdf"));
        Assert.False(resultado.IsValid);
    }
}
