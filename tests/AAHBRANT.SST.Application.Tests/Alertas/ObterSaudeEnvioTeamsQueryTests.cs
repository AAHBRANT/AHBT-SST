using AAHBRANT.SST.Application.Alertas.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

public class ObterSaudeEnvioTeamsQueryTests
{
    private static readonly DateTime Base = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    // CreatedAtUtc é sobrescrito pela auditoria no Add; grava primeiro e ajusta a data depois.
    private static async Task AdicionarEnviosAsync(IAppDbContext db, params (bool Sucesso, int MinutosDepois, string Canal)[] envios)
    {
        var entidades = envios
            .Select(e => new AlertaHistoricoEnvio
            {
                AlertaId = Guid.NewGuid(),
                Canal = e.Canal,
                Sucesso = e.Sucesso,
                MensagemErro = e.Sucesso ? null : "AADSTS7000215: Invalid client secret provided.",
            })
            .ToList();
        db.AlertaHistoricoEnvios.AddRange(entidades);
        await db.SaveChangesAsync();
        for (var i = 0; i < envios.Length; i++)
            entidades[i].CreatedAtUtc = Base.AddMinutes(envios[i].MinutosDepois);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SemHistorico_NaoEstaFalhando()
    {
        var db = CriarDb(nameof(SemHistorico_NaoEstaFalhando));

        var saude = await new ObterSaudeEnvioTeamsQueryHandler(db).Handle(new ObterSaudeEnvioTeamsQuery(), default);

        Assert.False(saude.Falhando);
    }

    [Fact]
    public async Task FalhasDepoisDoUltimoSucesso_EstaFalhandoEContaSoAsRecentes()
    {
        var db = CriarDb(nameof(FalhasDepoisDoUltimoSucesso_EstaFalhandoEContaSoAsRecentes));
        await AdicionarEnviosAsync(db,
            (false, 0, "ActivityFeed"),
            (true, 10, "ActivityFeed"),
            (false, 20, "ActivityFeed"),
            (false, 30, "ActivityFeed"));

        var saude = await new ObterSaudeEnvioTeamsQueryHandler(db).Handle(new ObterSaudeEnvioTeamsQuery(), default);

        Assert.True(saude.Falhando);
        Assert.Equal(2, saude.FalhasDesdeUltimoSucesso);
        Assert.Equal(Base.AddMinutes(30), saude.UltimaFalhaEmUtc);
        Assert.Equal(Base.AddMinutes(10), saude.UltimoSucessoEmUtc);
        Assert.Contains("AADSTS7000215", saude.UltimoErro);
    }

    [Fact]
    public async Task SucessoDepoisDasFalhas_AvisoSome()
    {
        var db = CriarDb(nameof(SucessoDepoisDasFalhas_AvisoSome));
        await AdicionarEnviosAsync(db,
            (false, 0, "ActivityFeed"),
            (false, 5, "ActivityFeed"),
            (true, 10, "ActivityFeed"));

        var saude = await new ObterSaudeEnvioTeamsQueryHandler(db).Handle(new ObterSaudeEnvioTeamsQuery(), default);

        Assert.False(saude.Falhando);
        Assert.Equal(Base.AddMinutes(10), saude.UltimoSucessoEmUtc);
    }

    [Fact]
    public async Task FalhaDeOutroCanal_NaoContaParaOSininho()
    {
        var db = CriarDb(nameof(FalhaDeOutroCanal_NaoContaParaOSininho));
        await AdicionarEnviosAsync(db,
            (true, 0, "ActivityFeed"),
            (false, 10, "Email"));

        var saude = await new ObterSaudeEnvioTeamsQueryHandler(db).Handle(new ObterSaudeEnvioTeamsQuery(), default);

        Assert.False(saude.Falhando);
    }
}
