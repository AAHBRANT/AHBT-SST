using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using System.Text.Json;

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
        Assert.Equal(DateTimeKind.Utc, resultado.EmitidoEm.Kind);
        Assert.False(resultado.Assinado);
        Assert.Empty(resultado.Signatarios);
    }

    [Fact]
    public async Task Handle_DocumentoFinalizadoComSignatario_ResolveComAssinadoTrue()
    {
        using var db = DbContextFactory.Criar();
        var funcao = new Funcao { Nome = "Servente de Obras" };
        var trabalhador = new Trabalhador { Nome = "Maria Teste", Cpf = "11122233344", Funcao = funcao, DataAdmissao = DateTime.UtcNow };
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
        documento.Signatarios.Add(new DocumentoSignatario
        {
            TrabalhadorId = trabalhador.Id,
            MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria,
            AssinadoEm = finalizadoEm,
            IpAddress = "187.19.184.130",
        });
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        var handler = new ResolverDocumentoPublicoQueryHandler(db);

        var resultado = await handler.Handle(new ResolverDocumentoPublicoQuery("TOKEN456"), default);

        Assert.NotNull(resultado);
        Assert.Equal(finalizadoEm, resultado!.EmitidoEm);
        Assert.Equal(DateTimeKind.Utc, resultado.EmitidoEm.Kind);
        Assert.Contains("\"emitidoEm\":\"2026-09-03T08:00:00Z\"", JsonSerializer.Serialize(resultado, JsonOptions));
        Assert.True(resultado.Assinado);
        Assert.Single(resultado.Signatarios);
        Assert.Equal("Maria Teste", resultado.Signatarios[0].TrabalhadorNome);
        Assert.Equal(DateTimeKind.Utc, resultado.Signatarios[0].AssinadoEm.Kind);
        Assert.Contains("\"assinadoEm\":\"2026-09-03T08:00:00Z\"", JsonSerializer.Serialize(resultado, JsonOptions));
        Assert.Equal("***.***.***-44", resultado.Signatarios[0].TrabalhadorCpfMascarado);
        Assert.Equal("Servente de Obras", resultado.Signatarios[0].TrabalhadorFuncaoNome);
        Assert.Equal("187.19.184.xxx", resultado.Signatarios[0].OrigemRede);
    }

    // A Ficha de EPI é documento consolidado: o registro dela nunca recebe signatário (ver
    // TipoDocumentoAssinatura), então a página pública precisa mostrar as assinaturas das entregas
    // que ela agrega — senão afirma "nenhuma assinatura" sobre um PDF assinado linha a linha.
    [Fact]
    public async Task Handle_FichaEpiConsolidada_AgregaAssinaturasDasEntregasDoTrabalhador()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = new Trabalhador
        {
            Nome = "João Teste",
            Cpf = "11122233344",
            Funcao = new Funcao { Nome = "Ajudante" },
            DataAdmissao = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(trabalhador);
        var entrega = new EntregaEpi { TrabalhadorId = trabalhador.Id, Quantidade = 1, DataEntrega = DateTime.UtcNow };
        db.EntregasEpi.Add(entrega);
        await db.SaveChangesAsync();

        var assinadoEm = new DateTime(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc);
        var documentoEntrega = new DocumentoAssinatura
        {
            EntidadeTipo = "EntregaEpi",
            EntidadeId = entrega.Id,
            Status = StatusDocumentoAssinatura.Finalizado,
            ConteudoHash = "HASH-ENTREGA",
            FinalizadoEm = assinadoEm,
        };
        documentoEntrega.Signatarios.Add(new DocumentoSignatario
        {
            TrabalhadorId = trabalhador.Id,
            MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria,
            AssinadoEm = assinadoEm,
        });
        db.DocumentosAssinatura.Add(documentoEntrega);

        // Registro da Ficha: só rastreabilidade, sem signatário próprio.
        db.DocumentosAssinatura.Add(new DocumentoAssinatura
        {
            EntidadeTipo = "FichaEpiTrabalhador",
            EntidadeId = trabalhador.Id,
            Status = StatusDocumentoAssinatura.EmAndamento,
            TokenValidacaoPublica = "TOKEN-FICHA",
            ConteudoHash = "HASH-FICHA",
            RastreadoEm = assinadoEm,
        });
        await db.SaveChangesAsync();
        var handler = new ResolverDocumentoPublicoQueryHandler(db);

        var resultado = await handler.Handle(new ResolverDocumentoPublicoQuery("TOKEN-FICHA"), default);

        Assert.NotNull(resultado);
        Assert.True(resultado!.Consolidado);
        Assert.Equal("Ficha de EPI", resultado.EntidadeTipoRotulo);
        Assert.NotNull(resultado.OrigemAssinaturas);
        Assert.True(resultado.Assinado);
        Assert.Single(resultado.Signatarios);
        Assert.Equal("João Teste", resultado.Signatarios[0].TrabalhadorNome);
    }

    // Documento comum sem assinatura continua dizendo que não tem — a frase de documento
    // consolidado não pode vazar para tipos que de fato assinam a si próprios.
    [Fact]
    public async Task Handle_DocumentoComumSemSignatario_NaoRecebeTratamentoDeConsolidado()
    {
        using var db = DbContextFactory.Criar();
        db.DocumentosAssinatura.Add(new DocumentoAssinatura
        {
            EntidadeTipo = "Apr",
            EntidadeId = Guid.NewGuid(),
            Status = StatusDocumentoAssinatura.EmAndamento,
            TokenValidacaoPublica = "TOKEN-APR",
            ConteudoHash = "HASH-APR",
            RastreadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var handler = new ResolverDocumentoPublicoQueryHandler(db);

        var resultado = await handler.Handle(new ResolverDocumentoPublicoQuery("TOKEN-APR"), default);

        Assert.NotNull(resultado);
        Assert.False(resultado!.Consolidado);
        Assert.Null(resultado.OrigemAssinaturas);
        Assert.Equal("APR — Análise Preliminar de Risco", resultado.EntidadeTipoRotulo);
        Assert.Empty(resultado.Signatarios);
    }

    // Integridade de conteúdo: a página precisa entregar o SHA-256 do arquivo emitido, senão quem
    // escaneia o QR não tem como distinguir o documento original de um adulterado.
    [Fact]
    public async Task Handle_DocumentoComArquivoRegistrado_ExpoeHashDoArquivo()
    {
        using var db = DbContextFactory.Criar();
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 };
        var hashEsperado = HashArquivoCalculador.Calcular(pdf);
        db.DocumentosAssinatura.Add(new DocumentoAssinatura
        {
            EntidadeTipo = "Dds",
            EntidadeId = Guid.NewGuid(),
            Status = StatusDocumentoAssinatura.EmAndamento,
            TokenValidacaoPublica = "TOKEN-ARQUIVO",
            ConteudoHash = "HASH-SIGNATARIOS",
            RastreadoEm = DateTime.UtcNow,
            PdfConteudo = pdf,
            HashPdf = hashEsperado,
            ArquivoAtualizadoEm = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();
        var handler = new ResolverDocumentoPublicoQueryHandler(db);

        var resultado = await handler.Handle(new ResolverDocumentoPublicoQuery("TOKEN-ARQUIVO"), default);

        Assert.NotNull(resultado);
        Assert.NotNull(resultado);
        Assert.Equal(hashEsperado, resultado.HashPdf);
        Assert.NotNull(resultado.ArquivoAtualizadoEm);
        Assert.Equal(DateTimeKind.Utc, resultado.ArquivoAtualizadoEm.Value.Kind);
        // O hash das assinaturas continua existindo e é outro valor — são provas distintas.
        Assert.Equal("HASH-SIGNATARIOS", resultado.ConteudoHash);
        Assert.NotEqual(resultado.ConteudoHash, resultado.HashPdf);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Handle_DocumentoSemArquivoRegistrado_NaoExpoeHashDoArquivo()
    {
        using var db = DbContextFactory.Criar();
        db.DocumentosAssinatura.Add(new DocumentoAssinatura
        {
            EntidadeTipo = "Dds",
            EntidadeId = Guid.NewGuid(),
            Status = StatusDocumentoAssinatura.EmAndamento,
            TokenValidacaoPublica = "TOKEN-SEM-ARQUIVO",
            ConteudoHash = "HASH",
            RastreadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var handler = new ResolverDocumentoPublicoQueryHandler(db);

        var resultado = await handler.Handle(new ResolverDocumentoPublicoQuery("TOKEN-SEM-ARQUIVO"), default);

        Assert.NotNull(resultado);
        Assert.Null(resultado.HashPdf);
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
