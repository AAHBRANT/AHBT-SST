using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

// Ação de plano avisa quem tem que fazer (o responsável da ação), não o responsável fixo do módulo;
// sem responsável na ação, o fixo continua valendo (08/10/2026).
public class AlertaDestinatarioDoItemTests
{
    [Fact]
    public async Task DestinatarioDoItem_TemPrecedenciaSobreResponsavelDaRegra()
    {
        var db = DbContextFactory.Criar();
        var responsavelFixo = new Usuario { Email = "fixo@x.com", Nome = "Fixo do módulo" };
        var responsavelAcao = new Usuario { Email = "acao@x.com", Nome = "Responsável da ação" };
        db.AddRange(responsavelFixo, responsavelAcao, new RegraAlerta
        {
            Modulo = TipoModuloAlerta.PlanoAcao,
            DiasAntecedencia = 5,
            Severidade = SeveridadeAlerta.Atencao,
            ResponsavelUsuarioId = responsavelFixo.Id,
        });
        await db.SaveChangesAsync();

        var comResponsavel = Guid.NewGuid();
        var semResponsavel = Guid.NewGuid();
        var provider = new AlertaOrigemProviderFalso
        {
            Modulo = TipoModuloAlerta.PlanoAcao,
            Itens = new List<AlertaOrigemItem>
            {
                Item(comResponsavel, responsavelAcao.Id),
                Item(semResponsavel, null),
            },
        };
        var filaTeams = new FilaNotificacaoTeamsFalsa();
        await new AlertaEngineService(db, new List<IAlertaOrigemProvider> { provider }, filaTeams, new FilaCalendarioTeamsFalsa())
            .ProcessarAsync();

        var alertas = await db.Alertas.ToListAsync();
        Assert.Equal(responsavelAcao.Id, alertas.Single(a => a.EntidadeOrigemId == comResponsavel).DestinatarioUsuarioId);
        Assert.Equal(responsavelFixo.Id, alertas.Single(a => a.EntidadeOrigemId == semResponsavel).DestinatarioUsuarioId);
    }

    private static AlertaOrigemItem Item(Guid id, Guid? destinatario) => new()
    {
        EntidadeOrigemTipo = nameof(AcaoPlano),
        EntidadeOrigemId = id,
        DataVencimento = DateTime.UtcNow.Date.AddDays(2),
        TipoAlertaVencendo = TipoAlerta.AcaoAtrasada,
        Titulo = "Ação",
        DestinatarioUsuarioId = destinatario,
    };
}
