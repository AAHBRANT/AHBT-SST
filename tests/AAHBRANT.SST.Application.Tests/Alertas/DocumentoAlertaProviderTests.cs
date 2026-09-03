using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

public class DocumentoAlertaProviderTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static Obra CriarObra() => new() { Codigo = "OBRA-1", Nome = "Obra Teste" };

    [Fact]
    public async Task ObterItensAsync_PcmsoVigenteComValidade_GeraItem()
    {
        var db = CriarDb(nameof(ObterItensAsync_PcmsoVigenteComValidade_GeraItem));
        var obra = CriarObra();
        db.Obras.Add(obra);
        var pcmso = new PcmsoDetalhe
        {
            Nome = "PCMSO Teste",
            ObraId = obra.Id,
            DataEmissao = DateTime.UtcNow.AddYears(-1),
            Validade = DateTime.UtcNow.AddDays(10),
            Status = StatusPcmsoDocumento.Vigente,
        };
        db.PcmsoDetalhes.Add(pcmso);
        await db.SaveChangesAsync();

        var item = Assert.Single(await new DocumentoAlertaProvider(db).ObterItensAsync());

        Assert.Equal("Pcmso", item.EntidadeOrigemTipo);
        Assert.Equal(pcmso.Id, item.EntidadeOrigemId);
        Assert.Equal(pcmso.Validade, item.DataVencimento);
        Assert.Equal(TipoAlerta.DocumentoVencendo, item.TipoAlertaVencendo);
        Assert.Equal(TipoAlerta.DocumentoVencido, item.TipoAlertaVencido);
        Assert.Equal(obra.Id, item.ObraId);
    }

    [Fact]
    public async Task ObterItensAsync_PcmsoSemValidade_NaoGeraItem()
    {
        var db = CriarDb(nameof(ObterItensAsync_PcmsoSemValidade_NaoGeraItem));
        db.PcmsoDetalhes.Add(new PcmsoDetalhe
        {
            Nome = "PCMSO sem validade",
            DataEmissao = DateTime.UtcNow,
            Validade = null,
            Status = StatusPcmsoDocumento.Rascunho,
        });
        await db.SaveChangesAsync();

        var itens = await new DocumentoAlertaProvider(db).ObterItensAsync();

        Assert.Empty(itens);
    }

    [Theory]
    [InlineData(StatusPcmsoDocumento.Obsoleto)]
    [InlineData(StatusPcmsoDocumento.Cancelado)]
    public async Task ObterItensAsync_PcmsoObsoletoOuCancelado_NaoGeraItem(StatusPcmsoDocumento status)
    {
        var db = CriarDb(nameof(ObterItensAsync_PcmsoObsoletoOuCancelado_NaoGeraItem) + status);
        db.PcmsoDetalhes.Add(new PcmsoDetalhe
        {
            Nome = "PCMSO fora de vigência",
            DataEmissao = DateTime.UtcNow.AddYears(-2),
            Validade = DateTime.UtcNow.AddDays(-5),
            Status = status,
        });
        await db.SaveChangesAsync();

        var itens = await new DocumentoAlertaProvider(db).ObterItensAsync();

        Assert.Empty(itens);
    }
}
