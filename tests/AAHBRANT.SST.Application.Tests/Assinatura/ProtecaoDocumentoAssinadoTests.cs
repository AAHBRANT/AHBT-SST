using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

public class ProtecaoDocumentoAssinadoTests
{
    private static async Task<(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext Db, Guid EntidadeId)> PrepararAsync(bool assinado, bool finalizado)
    {
        var db = DbContextFactory.Criar();
        var entidadeId = Guid.NewGuid();
        var documento = new DocumentoAssinatura
        {
            EntidadeTipo = "EntregaEpi", EntidadeId = entidadeId,
            Status = finalizado ? StatusDocumentoAssinatura.Finalizado : StatusDocumentoAssinatura.EmAndamento,
        };
        db.DocumentosAssinatura.Add(documento);
        if (assinado)
            db.DocumentoSignatarios.Add(new DocumentoSignatario { DocumentoAssinaturaId = documento.Id, TrabalhadorId = Guid.NewGuid(), MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria, AssinadoEm = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return (db, entidadeId);
    }

    [Fact]
    public async Task EntregaAssinada_NaoPodeSerAlterada()
    {
        var (db, id) = await PrepararAsync(assinado: true, finalizado: false);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProtecaoDocumentoAssinado.GarantirEditavelAsync(db, id, ProtecaoDocumentoAssinado.Trava.AposPrimeiraAssinatura, default, "EntregaEpi", "DevolucaoEpi"));
        Assert.Contains("já foi assinado", erro.Message);
    }

    [Fact]
    public async Task SemAssinatura_PodeSerAlterada()
    {
        var (db, id) = await PrepararAsync(assinado: false, finalizado: false);
        await ProtecaoDocumentoAssinado.GarantirEditavelAsync(db, id, ProtecaoDocumentoAssinado.Trava.AposPrimeiraAssinatura, default, "EntregaEpi");
    }

    // Documento coletivo (ex.: APR) segue editável enquanto recebe assinaturas; trava só ao finalizar.
    [Fact]
    public async Task TravaAposFinalizacao_AssinadoMasNaoFinalizado_PodeSerAlterado()
    {
        var (db, id) = await PrepararAsync(assinado: true, finalizado: false);
        await ProtecaoDocumentoAssinado.GarantirEditavelAsync(db, id, ProtecaoDocumentoAssinado.Trava.AposFinalizacao, default, "EntregaEpi");
    }

    [Fact]
    public async Task TravaAposFinalizacao_Finalizado_NaoPodeSerAlterado()
    {
        var (db, id) = await PrepararAsync(assinado: true, finalizado: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProtecaoDocumentoAssinado.GarantirEditavelAsync(db, id, ProtecaoDocumentoAssinado.Trava.AposFinalizacao, default, "EntregaEpi"));
    }
}
