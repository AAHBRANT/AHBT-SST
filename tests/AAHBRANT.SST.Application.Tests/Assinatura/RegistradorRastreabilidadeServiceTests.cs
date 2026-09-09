using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Assinatura;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

public class QrCodeDocumentoServiceFalso : IQrCodeDocumentoService
{
    public QrCodeDocumentoResultado Gerar(string token) => new(new byte[] { 9, 9 }, $"https://fake/#/validar/{token}");
}

public class RegistradorRastreabilidadeServiceTests
{
    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task GarantirAsync_DocumentoNovo_CriaComTokenHashETemAssinaturaFalse()
    {
        var db = CriarDb(nameof(GarantirAsync_DocumentoNovo_CriaComTokenHashETemAssinaturaFalse));
        var servico = new RegistradorRastreabilidadeService(db, new QrCodeDocumentoServiceFalso());
        var entidadeId = Guid.NewGuid();

        var resultado = await servico.GarantirAsync("Cipa", entidadeId, default);

        Assert.False(resultado.TemAssinatura);
        Assert.NotEmpty(resultado.ConteudoHash);
        Assert.Contains("/validar/", resultado.UrlValidacaoPublica);

        var documento = await db.DocumentosAssinatura.SingleAsync(d => d.EntidadeTipo == "Cipa" && d.EntidadeId == entidadeId);
        Assert.NotNull(documento.TokenValidacaoPublica);
        Assert.NotNull(documento.RastreadoEm);
        Assert.Equal(StatusDocumentoAssinatura.EmAndamento, documento.Status);
    }

    [Fact]
    public async Task GarantirAsync_ChamadoDuasVezesEmAndamento_MantemMesmoTokenERecalculaHash()
    {
        var db = CriarDb(nameof(GarantirAsync_ChamadoDuasVezesEmAndamento_MantemMesmoTokenERecalculaHash));
        var servico = new RegistradorRastreabilidadeService(db, new QrCodeDocumentoServiceFalso());
        var entidadeId = Guid.NewGuid();

        var primeiro = await servico.GarantirAsync("Dds", entidadeId, default);
        var tokenApósPrimeiraChamada = (await db.DocumentosAssinatura.SingleAsync(d => d.EntidadeId == entidadeId)).TokenValidacaoPublica;

        // Simula um signatário aparecendo entre as duas chamadas (ex.: presença biométrica registrada
        // depois do primeiro export do PDF) — o hash deve refletir isso na segunda chamada.
        // Adicionado direto no DbSet (não via `documento.Signatarios.Add`): como DocumentoSignatario
        // já tem Id preenchido no momento da construção (AuditableEntity.Id = Guid.NewGuid() por
        // padrão) e o `documento` pai já está rastreado desde a 1ª chamada, o EF Core/InMemory marca a
        // entidade alcançada só por navegação como Modified em vez de Added (heurística: PK não-default
        // + não veio de um Add() explícito = "pode já existir") — daí o DbUpdateConcurrencyException.
        var documento = await db.DocumentosAssinatura.SingleAsync(d => d.EntidadeId == entidadeId);
        db.DocumentoSignatarios.Add(new DocumentoSignatario
        {
            DocumentoAssinaturaId = documento.Id,
            TrabalhadorId = Guid.NewGuid(),
            MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria,
            AssinadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var segundo = await servico.GarantirAsync("Dds", entidadeId, default);
        var tokenApósSegundaChamada = (await db.DocumentosAssinatura.SingleAsync(d => d.EntidadeId == entidadeId)).TokenValidacaoPublica;

        Assert.Equal(tokenApósPrimeiraChamada, tokenApósSegundaChamada);
        Assert.NotEqual(primeiro.ConteudoHash, segundo.ConteudoHash);
        Assert.True(segundo.TemAssinatura);
    }

    [Fact]
    public async Task GarantirAsync_DocumentoJaFinalizado_NaoAlteraHashNemToken()
    {
        var db = CriarDb(nameof(GarantirAsync_DocumentoJaFinalizado_NaoAlteraHashNemToken));
        var entidadeId = Guid.NewGuid();
        var documento = new DocumentoAssinatura
        {
            EntidadeTipo = "Treinamento",
            EntidadeId = entidadeId,
            Status = StatusDocumentoAssinatura.Finalizado,
            ConteudoHash = "HASHCONGELADO",
            TokenValidacaoPublica = "TOKENCONGELADO",
            FinalizadoEm = DateTime.UtcNow,
        };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        var servico = new RegistradorRastreabilidadeService(db, new QrCodeDocumentoServiceFalso());
        var resultado = await servico.GarantirAsync("Treinamento", entidadeId, default);

        Assert.Equal("HASHCONGELADO", resultado.ConteudoHash);
        Assert.Contains("TOKENCONGELADO", resultado.UrlValidacaoPublica);
    }
}
