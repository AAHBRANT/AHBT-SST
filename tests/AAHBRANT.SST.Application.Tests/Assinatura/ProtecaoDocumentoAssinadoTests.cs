using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.EntregasEpi.Commands;
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

    // A devolução é registrada alterando a entrega, quase sempre depois de a entrega estar assinada.
    private static async Task<(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext Db, EntregaEpi Entrega)> EntregaAssinadaAsync(bool devolucaoAssinada = false)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow };
        var entrega = new EntregaEpi { TrabalhadorId = trabalhador.Id, CatalogoEpiId = Guid.NewGuid(), Quantidade = 2, DataEntrega = DateTime.UtcNow.Date, MotivoTipo = MotivoEntregaEpi.Inicial };
        db.Obras.Add(obra);
        db.Trabalhadores.Add(trabalhador);
        db.EntregasEpi.Add(entrega);
        foreach (var tipo in devolucaoAssinada ? new[] { "EntregaEpi", "DevolucaoEpi" } : new[] { "EntregaEpi" })
        {
            var doc = new DocumentoAssinatura { EntidadeTipo = tipo, EntidadeId = entrega.Id };
            db.DocumentosAssinatura.Add(doc);
            db.DocumentoSignatarios.Add(new DocumentoSignatario { DocumentoAssinaturaId = doc.Id, TrabalhadorId = trabalhador.Id, MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria, AssinadoEm = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
        return (db, entrega);
    }

    private static AtualizarEntregaEpiCommand Comando(EntregaEpi e, int quantidade, DateTime? devolucao) => new(
        e.Id, e.TrabalhadorId, e.CatalogoEpiId, e.DataEntrega, devolucao, e.DataValidade, quantidade, devolucao is null ? null : quantidade,
        e.VistoConsorcioResponsavel, e.Motivo, e.Observacoes, MotivoEntregaEpi.Inicial, e.NumeroListaPresencaNr6, e.DataTreinamentoNr6);

    [Fact]
    public async Task EntregaAssinada_RegistrarDevolucao_Permitido()
    {
        var (db, entrega) = await EntregaAssinadaAsync();

        await new AtualizarEntregaEpiCommandHandler(db).Handle(Comando(entrega, 2, DateTime.UtcNow.Date), default);

        Assert.NotNull(db.EntregasEpi.Single(e => e.Id == entrega.Id).DataDevolucao);
    }

    [Fact]
    public async Task EntregaAssinada_AlterarQuantidade_Recusado()
    {
        var (db, entrega) = await EntregaAssinadaAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AtualizarEntregaEpiCommandHandler(db).Handle(Comando(entrega, 5, null), default));
        Assert.Contains("só é possível registrar a devolução", erro.Message);
    }

    [Fact]
    public async Task DevolucaoAssinada_NaoPodeMaisAlterar()
    {
        var (db, entrega) = await EntregaAssinadaAsync(devolucaoAssinada: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AtualizarEntregaEpiCommandHandler(db).Handle(Comando(entrega, 2, DateTime.UtcNow.Date), default));
    }

    [Fact]
    public async Task ListarTravados_DevolveSoOsAssinados()
    {
        var (db, entrega) = await EntregaAssinadaAsync();
        var livre = Guid.NewGuid();

        var travados = await ProtecaoDocumentoAssinado.ListarTravadosAsync(db, "EntregaEpi", new[] { entrega.Id, livre }, default);

        Assert.Equal(new[] { entrega.Id }, travados);
    }
}
