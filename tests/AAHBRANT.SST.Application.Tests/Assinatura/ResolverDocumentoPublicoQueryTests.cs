using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

public class ResolverDocumentoPublicoQueryTests
{
    [Fact]
    public async Task Handle_DocumentoEmAndamentoSemFinalizadoEm_ResolvePorTokenUsandoRastreadoEmComoEmitidoEm()
    {
        using var db = DbContextFactory.Criar();
        var rastreadoEm = new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
        db.DocumentosAssinatura.Add(new DocumentoAssinatura
        {
            EntidadeTipo = "Cipa",
            EntidadeId = Guid.NewGuid(),
            Status = StatusDocumentoAssinatura.EmAndamento,
            TokenValidacaoPublica = "TOKEN123",
            ConteudoHash = "HASHABC",
            RastreadoEm = rastreadoEm,
        });
        await db.SaveChangesAsync();
        var handler = new ResolverDocumentoPublicoQueryHandler(db);

        var resultado = await handler.Handle(new ResolverDocumentoPublicoQuery("TOKEN123"), default);

        Assert.NotNull(resultado);
        Assert.Equal(rastreadoEm, resultado!.EmitidoEm);
        Assert.False(resultado.Assinado);
        Assert.Empty(resultado.Signatarios);
    }

    [Fact]
    public async Task Handle_DocumentoFinalizadoComSignatario_ResolveComAssinadoTrue()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = new Trabalhador { Nome = "Maria Teste", Cpf = "11122233344", DataAdmissao = DateTime.UtcNow };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var finalizadoEm = new DateTime(2026, 9, 3, 8, 0, 0, DateTimeKind.Utc);
        var documento = new DocumentoAssinatura
        {
            EntidadeTipo = "Dds",
            EntidadeId = Guid.NewGuid(),
            Status = StatusDocumentoAssinatura.Finalizado,
            TokenValidacaoPublica = "TOKEN456",
            ConteudoHash = "HASHDEF",
            FinalizadoEm = finalizadoEm,
        };
        documento.Signatarios.Add(new DocumentoSignatario { TrabalhadorId = trabalhador.Id, MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria, AssinadoEm = finalizadoEm });
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        var handler = new ResolverDocumentoPublicoQueryHandler(db);

        var resultado = await handler.Handle(new ResolverDocumentoPublicoQuery("TOKEN456"), default);

        Assert.NotNull(resultado);
        Assert.Equal(finalizadoEm, resultado!.EmitidoEm);
        Assert.True(resultado.Assinado);
        Assert.Single(resultado.Signatarios);
    }

    [Fact]
    public async Task Handle_TokenInexistente_RetornaNull()
    {
        using var db = DbContextFactory.Criar();
        var handler = new ResolverDocumentoPublicoQueryHandler(db);

        var resultado = await handler.Handle(new ResolverDocumentoPublicoQuery("NAO-EXISTE"), default);

        Assert.Null(resultado);
    }
}
