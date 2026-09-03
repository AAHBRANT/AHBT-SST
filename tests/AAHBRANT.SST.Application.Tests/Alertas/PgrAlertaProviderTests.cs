using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

public class PgrAlertaProviderTests
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
    public async Task ObterItensAsync_PgrComTerminoERevisao_GeraDoisItensDistintos()
    {
        var db = CriarDb(nameof(ObterItensAsync_PgrComTerminoERevisao_GeraDoisItensDistintos));
        var obra = CriarObra();
        db.Obras.Add(obra);
        var pgr = new Pgr
        {
            ObraId = obra.Id,
            Nome = "PGR Teste",
            DataElaboracao = DateTime.UtcNow.AddYears(-1),
            DataProximaRevisao = DateTime.UtcNow.AddDays(10),
            DataTermino = DateTime.UtcNow.AddDays(20),
            Status = StatusPgr.Vigente,
        };
        db.Pgrs.Add(pgr);
        await db.SaveChangesAsync();

        var itens = await new PgrAlertaProvider(db).ObterItensAsync();

        Assert.Equal(2, itens.Count);

        var itemTermino = Assert.Single(itens, i => i.EntidadeOrigemTipo == "PgrTermino");
        Assert.Equal(pgr.Id, itemTermino.EntidadeOrigemId);
        Assert.Equal(pgr.DataTermino, itemTermino.DataVencimento);
        Assert.Equal(TipoAlerta.PgrVencendo, itemTermino.TipoAlertaVencendo);
        Assert.Equal(TipoAlerta.PgrVencido, itemTermino.TipoAlertaVencido);
        Assert.Equal(obra.Id, itemTermino.ObraId);

        var itemRevisao = Assert.Single(itens, i => i.EntidadeOrigemTipo == "PgrRevisao");
        Assert.Equal(pgr.Id, itemRevisao.EntidadeOrigemId);
        Assert.Equal(pgr.DataProximaRevisao, itemRevisao.DataVencimento);
        Assert.Equal(TipoAlerta.PgrRevisaoVencendo, itemRevisao.TipoAlertaVencendo);
        Assert.Equal(TipoAlerta.PgrRevisaoVencida, itemRevisao.TipoAlertaVencido);
    }

    [Fact]
    public async Task ObterItensAsync_PgrSemDataTermino_NaoGeraItemDeTermino()
    {
        var db = CriarDb(nameof(ObterItensAsync_PgrSemDataTermino_NaoGeraItemDeTermino));
        var obra = CriarObra();
        db.Obras.Add(obra);
        db.Pgrs.Add(new Pgr
        {
            ObraId = obra.Id,
            Nome = "PGR sem término",
            DataElaboracao = DateTime.UtcNow,
            DataProximaRevisao = DateTime.UtcNow.AddDays(5),
            DataTermino = null,
            Status = StatusPgr.EmElaboracao,
        });
        await db.SaveChangesAsync();

        var item = Assert.Single(await new PgrAlertaProvider(db).ObterItensAsync());

        Assert.Equal("PgrRevisao", item.EntidadeOrigemTipo);
    }

    [Fact]
    public async Task ObterItensAsync_PgrEncerrado_NaoGeraNenhumItem()
    {
        var db = CriarDb(nameof(ObterItensAsync_PgrEncerrado_NaoGeraNenhumItem));
        var obra = CriarObra();
        db.Obras.Add(obra);
        db.Pgrs.Add(new Pgr
        {
            ObraId = obra.Id,
            Nome = "PGR encerrado",
            DataElaboracao = DateTime.UtcNow.AddYears(-2),
            DataProximaRevisao = DateTime.UtcNow.AddDays(-400),
            DataTermino = DateTime.UtcNow.AddDays(-1),
            Status = StatusPgr.Encerrado,
        });
        await db.SaveChangesAsync();

        var itens = await new PgrAlertaProvider(db).ObterItensAsync();

        Assert.Empty(itens);
    }
}
