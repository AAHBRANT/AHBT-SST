using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

// Alertas antigos gravavam a mesma (EntidadeOrigemTipo, EntidadeOrigemId) para cada técnico. O motor não pode
// quebrar (e derrubar todas as obras) com chave repetida entre alertas em aberto.
public class AlertaEngineChaveDuplicadaTests
{
    private static IAppDbContext CriarDb(string nomeBanco) =>
        new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options, new CurrentUserService());

    private static async Task<(IAppDbContext Db, AlertaEngineService Engine, AlertaOrigemProviderFalso Provider, Alerta Antigo, Alerta Recente)> MontarAsync(string banco)
    {
        var db = CriarDb(banco);
        db.RegrasAlerta.Add(new RegraAlerta { Modulo = TipoModuloAlerta.Aso, DiasAntecedencia = 30, Severidade = SeveridadeAlerta.Atencao });
        var origemId = Guid.NewGuid();
        var antigo = NovoAlerta(origemId);
        db.Alertas.Add(antigo);
        await db.SaveChangesAsync();
        await Task.Delay(10);
        var recente = NovoAlerta(origemId);
        db.Alertas.Add(recente);
        await db.SaveChangesAsync();

        var provider = new AlertaOrigemProviderFalso
        {
            Modulo = TipoModuloAlerta.Aso,
            Itens = new List<AlertaOrigemItem>
            {
                new()
                {
                    EntidadeOrigemTipo = "Aso", EntidadeOrigemId = origemId, DataVencimento = DateTime.UtcNow.Date.AddDays(10),
                    TipoAlertaVencendo = TipoAlerta.AsoVencendo, TipoAlertaVencido = TipoAlerta.AsoVencido, Titulo = "ASO João",
                },
            },
        };
        var engine = new AlertaEngineService(db, new List<IAlertaOrigemProvider> { provider }, new FilaNotificacaoTeamsFalsa(), new FilaCalendarioTeamsFalsa());
        return (db, engine, provider, antigo, recente);
    }

    private static Alerta NovoAlerta(Guid origemId) => new()
    {
        Tipo = TipoAlerta.AsoVencendo, Titulo = "Titulo antigo", EntidadeOrigemTipo = "Aso", EntidadeOrigemId = origemId,
        DestinatarioUsuarioId = Guid.NewGuid(),
    };

    [Fact]
    public async Task ChaveRepetidaEntreAbertos_NaoQuebra_EAtualizaOMaisRecente()
    {
        var (db, engine, _, antigo, recente) = await MontarAsync(nameof(ChaveRepetidaEntreAbertos_NaoQuebra_EAtualizaOMaisRecente));

        await engine.ProcessarAsync();

        var alertas = await db.Alertas.ToListAsync();
        Assert.Equal(2, alertas.Count); // nenhum alerta novo criado
        Assert.Contains("faltam 10 dia(s)", alertas.Single(a => a.Id == recente.Id).Titulo);
        Assert.Equal("Titulo antigo", alertas.Single(a => a.Id == antigo.Id).Titulo);
    }

    [Fact]
    public async Task ChaveRepetidaEntreAbertos_ItemSubstituido_ResolveOMaisRecente()
    {
        var (db, engine, provider, antigo, recente) = await MontarAsync(nameof(ChaveRepetidaEntreAbertos_ItemSubstituido_ResolveOMaisRecente));
        provider.Itens[0].Substituido = true;

        await engine.ProcessarAsync();

        var alertas = await db.Alertas.ToListAsync();
        Assert.Equal(StatusAlerta.Resolvido, alertas.Single(a => a.Id == recente.Id).Status);
        Assert.Equal(StatusAlerta.Aberto, alertas.Single(a => a.Id == antigo.Id).Status);
    }
}
