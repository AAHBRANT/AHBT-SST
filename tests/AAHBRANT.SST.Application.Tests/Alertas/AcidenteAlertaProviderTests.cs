using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Acidentes.Commands;
using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

public class AcidenteAlertaProviderTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static Acidente NovoAcidente(Guid obraId, GravidadeAcidente gravidade = GravidadeAcidente.SemAfastamento) => new()
    {
        ObraId = obraId,
        Tipo = TipoOcorrencia.Acidente,
        Local = "Frente 1",
        Data = DateTime.UtcNow.Date.AddDays(-3),
        Descricao = "Ocorrência de teste",
        Gravidade = gravidade,
    };

    // O DbContext carimba CreatedAtUtc ao inserir; para simular a data de registro, grava e depois ajusta.
    private static async Task SalvarAsync(IAppDbContext db, params Acidente[] acidentes)
        => await SalvarAsync(db, AvancarStatusAcidenteCommandHandler.ExigeTresFotosParaTodosOsTiposApartirDe.AddDays(1), acidentes);

    private static async Task SalvarAsync(IAppDbContext db, DateTime criadoEm, params Acidente[] acidentes)
    {
        await db.SaveChangesAsync();
        foreach (var a in acidentes) a.CreatedAtUtc = criadoEm;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Obito_Concluido_NaoAlerta()
    {
        var db = CriarDb(nameof(Obito_Concluido_NaoAlerta));
        var acidente = NovoAcidente(Guid.NewGuid(), GravidadeAcidente.Obito);
        acidente.Status = StatusAcidente.Concluido;
        db.Acidentes.Add(acidente);
        await SalvarAsync(db, acidente);

        Assert.Empty(await new AcidenteAlertaProvider(db).ObterItensAsync());
    }

    [Fact]
    public async Task Obito_AnteriorAoCorte_NaoAlerta()
    {
        var db = CriarDb(nameof(Obito_AnteriorAoCorte_NaoAlerta));
        var acidente = NovoAcidente(Guid.NewGuid(), GravidadeAcidente.Obito);
        db.Acidentes.Add(acidente);
        await SalvarAsync(db, AvancarStatusAcidenteCommandHandler.ExigeTresFotosParaTodosOsTiposApartirDe.AddSeconds(-1), acidente);

        Assert.Empty(await new AcidenteAlertaProvider(db).ObterItensAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Obito_ConcluidoOuAntigo_ComAlertaEmAberto_VoltaComoSubstituido(bool concluido)
    {
        var db = CriarDb(nameof(Obito_ConcluidoOuAntigo_ComAlertaEmAberto_VoltaComoSubstituido) + concluido);
        var acidente = NovoAcidente(Guid.NewGuid(), GravidadeAcidente.Obito);
        if (concluido) acidente.Status = StatusAcidente.Concluido;
        db.Acidentes.Add(acidente);
        db.Alertas.Add(new Alerta
        {
            Tipo = TipoAlerta.ObitoProvidenciasPendentes, Titulo = "x",
            EntidadeOrigemTipo = AcidenteAlertaProvider.OrigemObito, EntidadeOrigemId = acidente.Id,
        });
        var criadoEm = AvancarStatusAcidenteCommandHandler.ExigeTresFotosParaTodosOsTiposApartirDe;
        await SalvarAsync(db, concluido ? criadoEm.AddDays(1) : criadoEm.AddDays(-30), acidente);

        var item = Assert.Single(await new AcidenteAlertaProvider(db).ObterItensAsync());

        Assert.True(item.Substituido);
        Assert.Equal(acidente.Id, item.EntidadeOrigemId);
    }

    [Fact]
    public async Task Obito_AoConcluir_AlertaAbertoVoltaComoSubstituido()
    {
        var db = CriarDb(nameof(Obito_AoConcluir_AlertaAbertoVoltaComoSubstituido));
        var acidente = NovoAcidente(Guid.NewGuid(), GravidadeAcidente.Obito);
        db.Acidentes.Add(acidente);
        await SalvarAsync(db, acidente);
        var provider = new AcidenteAlertaProvider(db);
        Assert.False(Assert.Single(await provider.ObterItensAsync()).Substituido);

        db.Alertas.Add(new Alerta
        {
            Tipo = TipoAlerta.ObitoProvidenciasPendentes, Titulo = "x",
            EntidadeOrigemTipo = AcidenteAlertaProvider.OrigemObito, EntidadeOrigemId = acidente.Id,
        });
        acidente.Status = StatusAcidente.Concluido;
        await db.SaveChangesAsync();

        Assert.True(Assert.Single(await provider.ObterItensAsync()).Substituido);
    }

    [Fact]
    public async Task Obito_ComProvidenciasEmAberto_GeraItemComObraETextoDoJuridico()
    {
        var db = CriarDb(nameof(Obito_ComProvidenciasEmAberto_GeraItemComObraETextoDoJuridico));
        var obraId = Guid.NewGuid();
        var acidente = NovoAcidente(obraId, GravidadeAcidente.Obito);
        db.Acidentes.Add(acidente);
        await SalvarAsync(db, acidente);

        var itens = await new AcidenteAlertaProvider(db).ObterItensAsync();

        var item = Assert.Single(itens);
        Assert.Equal(AcidenteAlertaProvider.OrigemObito, item.EntidadeOrigemTipo);
        Assert.Equal(acidente.Id, item.EntidadeOrigemId);
        Assert.Equal(obraId, item.ObraId);
        Assert.False(item.Substituido);
        Assert.Equal(TipoAlerta.ObitoProvidenciasPendentes, item.TipoAlertaVencendo);
        Assert.Contains("jurídico", item.Descricao);
        Assert.True(item.DataVencimento.Date < DateTime.UtcNow.Date);
    }

    [Fact]
    public async Task Obito_PorPessoaExterna_GeraItem()
    {
        var db = CriarDb(nameof(Obito_PorPessoaExterna_GeraItem));
        var acidente = NovoAcidente(Guid.NewGuid());
        db.Acidentes.Add(acidente);
        db.AcidentesPessoasExternas.Add(new AcidentePessoaExterna
        {
            AcidenteId = acidente.Id, Nome = "Pessoa", Vinculo = VinculoPessoaExterna.Visitante, Resultado = ResultadoPessoaExterna.Obito,
        });
        await SalvarAsync(db, acidente);

        var itens = await new AcidenteAlertaProvider(db).ObterItensAsync();

        var item = Assert.Single(itens);
        Assert.Equal(AcidenteAlertaProvider.OrigemObito, item.EntidadeOrigemTipo);
        Assert.False(item.Substituido);
    }

    [Fact]
    public async Task Obito_ComTodasProvidenciasConcluidas_VoltaComoSubstituido()
    {
        var db = CriarDb(nameof(Obito_ComTodasProvidenciasConcluidas_VoltaComoSubstituido));
        var acidente = NovoAcidente(Guid.NewGuid(), GravidadeAcidente.Obito);
        acidente.ProvidenciasObitoJson = ProvidenciasObitoAcidente.Gravar(
            null, ProvidenciasObitoAcidente.CodigosValidos.Select(c => (c, true, (string?)null)), DateTime.UtcNow);
        db.Acidentes.Add(acidente);
        await SalvarAsync(db, acidente);

        var item = Assert.Single(await new AcidenteAlertaProvider(db).ObterItensAsync());

        Assert.True(item.Substituido);
    }

    [Fact]
    public async Task Laudo_MaisDeLimiteDias_ProduzItemJaPassadoDoPrazo()
    {
        var db = CriarDb(nameof(Laudo_MaisDeLimiteDias_ProduzItemJaPassadoDoPrazo));
        var acidente = NovoAcidente(Guid.NewGuid());
        acidente.Status = StatusAcidente.AguardandoLaudo;
        acidente.AguardandoLaudoDesde = DateTime.UtcNow.AddDays(-(AcidenteAlertaProvider.DiasAlertaLaudoAguardado + 5));
        db.Acidentes.Add(acidente);
        await db.SaveChangesAsync();

        var item = Assert.Single(await new AcidenteAlertaProvider(db).ObterItensAsync());

        Assert.Equal(AcidenteAlertaProvider.OrigemLaudo, item.EntidadeOrigemTipo);
        Assert.True(item.DataVencimento.Date < DateTime.UtcNow.Date);
        Assert.Contains("35 dias", item.Titulo);
    }

    [Fact]
    public async Task Laudo_DentroDoLimite_NaoPassaDoPrazo()
    {
        var db = CriarDb(nameof(Laudo_DentroDoLimite_NaoPassaDoPrazo));
        var acidente = NovoAcidente(Guid.NewGuid());
        acidente.Status = StatusAcidente.AguardandoLaudo;
        acidente.AguardandoLaudoDesde = DateTime.UtcNow.AddDays(-10);
        db.Acidentes.Add(acidente);
        await db.SaveChangesAsync();

        var item = Assert.Single(await new AcidenteAlertaProvider(db).ObterItensAsync());

        Assert.True(item.DataVencimento.Date >= DateTime.UtcNow.Date);
    }

    [Fact]
    public async Task OcorrenciaComum_EExcluida_NaoGeraItem()
    {
        var db = CriarDb(nameof(OcorrenciaComum_EExcluida_NaoGeraItem));
        var comum = NovoAcidente(Guid.NewGuid());
        var excluido = NovoAcidente(Guid.NewGuid(), GravidadeAcidente.Obito);
        excluido.Ativo = false;
        db.Acidentes.AddRange(comum, excluido);
        await SalvarAsync(db, comum, excluido);

        Assert.Empty(await new AcidenteAlertaProvider(db).ObterItensAsync());
    }

    [Fact]
    public async Task AlertaEmAbertoDeOcorrenciaQueSaiuDoCriterio_VoltaComoSubstituido()
    {
        var db = CriarDb(nameof(AlertaEmAbertoDeOcorrenciaQueSaiuDoCriterio_VoltaComoSubstituido));
        var idAntigo = Guid.NewGuid();
        db.Alertas.Add(new Alerta
        {
            Tipo = TipoAlerta.OcorrenciaAguardandoLaudo, Titulo = "x",
            EntidadeOrigemTipo = AcidenteAlertaProvider.OrigemLaudo, EntidadeOrigemId = idAntigo,
        });
        await db.SaveChangesAsync();

        var item = Assert.Single(await new AcidenteAlertaProvider(db).ObterItensAsync());

        Assert.Equal(idAntigo, item.EntidadeOrigemId);
        Assert.True(item.Substituido);
    }
}
