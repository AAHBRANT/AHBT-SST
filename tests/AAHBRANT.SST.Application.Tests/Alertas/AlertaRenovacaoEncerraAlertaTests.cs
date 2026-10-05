using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

// Bug de 05/10: ASO renovado continuava com o alerta "VENCIDO" do ASO antigo aberto, porque o motor só
// encerrava o alerta quando o próprio item saía do vencimento — e o ASO antigo continua vencido.
public class AlertaRenovacaoEncerraAlertaTests
{
    private static IAppDbContext CriarDb(string nomeBanco) =>
        new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options, new CurrentUserService());

    [Fact]
    public async Task AsoProvider_MarcaComoSubstituidoSoOAsoAntigoDoMesmoTrabalhador()
    {
        var db = CriarDb(nameof(AsoProvider_MarcaComoSubstituidoSoOAsoAntigoDoMesmoTrabalhador));
        var hoje = DateTime.UtcNow.Date;
        var trabalhadorRenovado = new Trabalhador { Id = Guid.NewGuid(), Nome = "Renovado" };
        var trabalhadorVencido = new Trabalhador { Id = Guid.NewGuid(), Nome = "SoVencido" };
        var antigo = new Aso { Id = Guid.NewGuid(), TrabalhadorId = trabalhadorRenovado.Id, DataExame = hoje.AddYears(-1), DataValidade = hoje.AddDays(-60) };
        var novo = new Aso { Id = Guid.NewGuid(), TrabalhadorId = trabalhadorRenovado.Id, DataExame = hoje.AddDays(-30), DataValidade = hoje.AddYears(1) };
        var unicoVencido = new Aso { Id = Guid.NewGuid(), TrabalhadorId = trabalhadorVencido.Id, DataExame = hoje.AddYears(-1), DataValidade = hoje.AddDays(-5) };
        db.Trabalhadores.AddRange(trabalhadorRenovado, trabalhadorVencido);
        db.Asos.AddRange(antigo, novo, unicoVencido);
        await db.SaveChangesAsync();

        var itens = await new AsoAlertaProvider(db).ObterItensAsync();

        Assert.True(itens.Single(i => i.EntidadeOrigemId == antigo.Id).Substituido);
        Assert.False(itens.Single(i => i.EntidadeOrigemId == novo.Id).Substituido);
        // Quem só tem ASO vencido (sem renovação) continua com o alerta.
        Assert.False(itens.Single(i => i.EntidadeOrigemId == unicoVencido.Id).Substituido);
    }

    [Fact]
    public async Task ProcessarAsync_ItemSubstituido_EncerraAlertaEmAbertoENaoCriaNovo()
    {
        var db = CriarDb(nameof(ProcessarAsync_ItemSubstituido_EncerraAlertaEmAbertoENaoCriaNovo));
        db.RegrasAlerta.Add(new RegraAlerta { Modulo = TipoModuloAlerta.Aso, DiasAntecedencia = 30, Severidade = SeveridadeAlerta.Atencao });
        var origemId = Guid.NewGuid();
        db.Alertas.Add(new Alerta
        {
            Tipo = TipoAlerta.AsoVencido,
            Severidade = SeveridadeAlerta.Critico,
            Titulo = "VENCIDO: ASO antigo",
            EntidadeOrigemTipo = "Aso",
            EntidadeOrigemId = origemId,
            Status = StatusAlerta.Aberto,
        });
        await db.SaveChangesAsync();

        var provider = new AlertaOrigemProviderFalso
        {
            Modulo = TipoModuloAlerta.Aso,
            Itens = new List<AlertaOrigemItem>
            {
                new()
                {
                    EntidadeOrigemTipo = "Aso",
                    EntidadeOrigemId = origemId,
                    DataVencimento = DateTime.UtcNow.Date.AddDays(-60),
                    TipoAlertaVencendo = TipoAlerta.AsoVencendo,
                    TipoAlertaVencido = TipoAlerta.AsoVencido,
                    Titulo = "ASO antigo",
                    Substituido = true,
                },
            },
        };
        var engine = new AlertaEngineService(db, new List<IAlertaOrigemProvider> { provider }, new FilaNotificacaoTeamsFalsa(), new FilaCalendarioTeamsFalsa());

        await engine.ProcessarAsync();

        var alerta = await db.Alertas.SingleAsync();
        Assert.Equal(StatusAlerta.Resolvido, alerta.Status);
    }
}
