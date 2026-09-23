using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Integridade de conteúdo (ver HashArquivoCalculador): guardar a cópia emitida e o SHA-256 dela é o
// que permite provar, depois, que o documento em mãos não foi adulterado — ConteudoHash sozinho
// cobre apenas a lista de signatários.
public class RegistradorRastreabilidadeArquivoTests
{
    private class QrCodeFalso : IQrCodeDocumentoService
    {
        public QrCodeDocumentoResultado Gerar(string token) =>
            new(Array.Empty<byte>(), $"https://validar.teste/#/validar/{token}");
    }

    [Fact]
    public async Task RegistrarArquivoAsync_DocumentoEmAndamento_GuardaArquivoEHashDosBytes()
    {
        using var db = DbContextFactory.Criar();
        var servico = new RegistradorRastreabilidadeService(db, new QrCodeFalso());
        var entidadeId = Guid.NewGuid();
        var rastreio = await servico.GarantirAsync("Dds", entidadeId, default);
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x01, 0x02 };

        await servico.RegistrarArquivoAsync(rastreio.DocumentoId, pdf, default);

        var documento = db.DocumentosAssinatura.Single(d => d.Id == rastreio.DocumentoId);
        Assert.Equal(pdf, documento.PdfConteudo);
        Assert.Equal(HashArquivoCalculador.Calcular(pdf), documento.HashPdf);
        Assert.NotNull(documento.ArquivoAtualizadoEm);
    }

    // Um byte diferente tem de produzir hash diferente — é exatamente essa propriedade que sustenta
    // a alegação de que o QR detecta adulteração do conteúdo.
    [Fact]
    public async Task RegistrarArquivoAsync_ConteudoAlterado_ProduzHashDiferente()
    {
        using var db = DbContextFactory.Criar();
        var servico = new RegistradorRastreabilidadeService(db, new QrCodeFalso());
        var rastreio = await servico.GarantirAsync("Apr", Guid.NewGuid(), default);

        await servico.RegistrarArquivoAsync(rastreio.DocumentoId, new byte[] { 1, 2, 3 }, default);
        var hashOriginal = db.DocumentosAssinatura.Single(d => d.Id == rastreio.DocumentoId).HashPdf;

        await servico.RegistrarArquivoAsync(rastreio.DocumentoId, new byte[] { 1, 2, 4 }, default);
        var hashAlterado = db.DocumentosAssinatura.Single(d => d.Id == rastreio.DocumentoId).HashPdf;

        Assert.NotEqual(hashOriginal, hashAlterado);
    }

    // Documento finalizado é prova congelada: uma reimpressão posterior não pode substituir a cópia
    // que foi assinada.
    [Fact]
    public async Task RegistrarArquivoAsync_DocumentoFinalizado_NaoSubstituiArquivoJaGuardado()
    {
        using var db = DbContextFactory.Criar();
        var original = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var documento = new DocumentoAssinatura
        {
            EntidadeTipo = "Inspecao",
            EntidadeId = Guid.NewGuid(),
            Status = StatusDocumentoAssinatura.Finalizado,
            TokenValidacaoPublica = "TOKEN-FINALIZADO",
            ConteudoHash = "HASH",
            FinalizadoEm = DateTime.UtcNow,
            PdfConteudo = original,
            HashPdf = HashArquivoCalculador.Calcular(original),
        };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        var servico = new RegistradorRastreabilidadeService(db, new QrCodeFalso());

        await servico.RegistrarArquivoAsync(documento.Id, new byte[] { 9, 9, 9, 9 }, default);

        var depois = db.DocumentosAssinatura.Single(d => d.Id == documento.Id);
        Assert.Equal(original, depois.PdfConteudo);
        Assert.Equal(HashArquivoCalculador.Calcular(original), depois.HashPdf);
    }
}
