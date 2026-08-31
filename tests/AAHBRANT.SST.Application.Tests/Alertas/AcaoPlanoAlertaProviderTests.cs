using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

public class AcaoPlanoAlertaProviderTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task ObterItensAsync_IgnoraAcoesConcluidasESemPrazo()
    {
        var db = CriarDb(nameof(ObterItensAsync_IgnoraAcoesConcluidasESemPrazo));
        db.AcoesPlano.AddRange(
            new AcaoPlano { OrigemTipo = "X", OrigemId = Guid.NewGuid(), Descricao = "concluida", Status = StatusControleRisco.Concluido, Prazo = DateTime.UtcNow.AddDays(-5) },
            new AcaoPlano { OrigemTipo = "X", OrigemId = Guid.NewGuid(), Descricao = "sem prazo", Status = StatusControleRisco.Pendente, Prazo = null });
        await db.SaveChangesAsync();
        var provider = new AcaoPlanoAlertaProvider(db);

        var itens = await provider.ObterItensAsync();

        Assert.Empty(itens);
    }

    [Fact]
    public async Task ObterItensAsync_OrigemNaoConformidade_ResolveObraIdViaAtividade()
    {
        var db = CriarDb(nameof(ObterItensAsync_OrigemNaoConformidade_ResolveObraIdViaAtividade));
        var obra = new Obra { Codigo = "OBR-1", Nome = "Obra 1" };
        db.Obras.Add(obra);
        var atividade = new Atividade { ObraId = obra.Id, Nome = "Montagem" };
        db.Atividades.Add(atividade);
        var nc = new NaoConformidade
        {
            Descricao = "Guarda-corpo ausente",
            OrigemDeteccao = OrigemNaoConformidade.Inspecao,
            AtividadeId = atividade.Id,
        };
        db.NaoConformidades.Add(nc);
        var acao = new AcaoPlano
        {
            OrigemTipo = nameof(NaoConformidade),
            OrigemId = nc.Id,
            Descricao = "Instalar guarda-corpo",
            Status = StatusControleRisco.Pendente,
            Prazo = DateTime.UtcNow.AddDays(-1),
        };
        db.AcoesPlano.Add(acao);
        await db.SaveChangesAsync();
        var provider = new AcaoPlanoAlertaProvider(db);

        var item = Assert.Single(await provider.ObterItensAsync());

        Assert.Equal(nameof(AcaoPlano), item.EntidadeOrigemTipo);
        Assert.Equal(acao.Id, item.EntidadeOrigemId);
        Assert.Equal(obra.Id, item.ObraId);
        Assert.Equal(TipoAlerta.AcaoAtrasada, item.TipoAlertaVencendo);
    }

    [Fact]
    public async Task ObterItensAsync_OrigemAcidente_ResolveObraIdDireto()
    {
        var db = CriarDb(nameof(ObterItensAsync_OrigemAcidente_ResolveObraIdDireto));
        var obra = new Obra { Codigo = "OBR-2", Nome = "Obra 2" };
        db.Obras.Add(obra);
        var acidente = new Acidente
        {
            Tipo = TipoOcorrencia.QuaseAcidente,
            ObraId = obra.Id,
            Local = "Térreo",
            Data = DateTime.UtcNow.Date,
            Descricao = "Queda de material",
        };
        db.Acidentes.Add(acidente);
        var acao = new AcaoPlano
        {
            OrigemTipo = nameof(Domain.Entidades.Acidente),
            OrigemId = acidente.Id,
            Descricao = "Isolar área",
            Status = StatusControleRisco.EmAndamento,
            Prazo = DateTime.UtcNow.AddDays(-2),
        };
        db.AcoesPlano.Add(acao);
        await db.SaveChangesAsync();
        var provider = new AcaoPlanoAlertaProvider(db);

        var item = Assert.Single(await provider.ObterItensAsync());

        Assert.Equal(obra.Id, item.ObraId);
    }
}
