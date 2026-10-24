using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

// Inspeção periódica de ponta a ponta (24/10/2026): o motor cria o alerta para o responsável do
// veículo quando vence, na obra dele, e encerra o alerta sozinho quando a inspeção é concluída.
public class InspecaoPeriodicaAlertaMotorTests
{
    [Fact]
    public async Task VeiculoVencido_AlertaOResponsavelNaObra_EConcluirAInspecaoEncerraOAlerta()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        var gilton = new Usuario { Email = "g@x.com", Nome = "Gilton" };
        var veiculo = new Veiculo { ObraId = obra.Id, Tipo = TipoVeiculo.CaminhaoMunck, PlacaPrefixo = "AAA1A11", PeriodicidadeInspecaoDias = 30, ResponsavelInspecaoUsuarioId = gilton.Id };
        db.AddRange(obra, gilton, veiculo);
        await db.SaveChangesAsync();
        // Última inspeção concluída há 40 dias: venceu há 10.
        var antiga = new Inspecao
        {
            ObraId = obra.Id, TipoInspecao = TipoInspecao.Veiculo, VeiculoId = veiculo.Id, Status = StatusInspecao.Concluida,
            Data = DateTime.UtcNow.AddDays(-40), ChecklistModeloId = Guid.NewGuid(), ResponsavelUsuarioId = gilton.Id,
        };
        db.Add(antiga);
        await db.SaveChangesAsync();

        var motor = new AlertaEngineService(db, new List<IAlertaOrigemProvider> { new InspecaoPeriodicaAlertaProvider(db) },
            new FilaNotificacaoTeamsFalsa(), new FilaCalendarioTeamsFalsa());
        await motor.ProcessarAsync();

        var alerta = await db.Alertas.SingleAsync();
        Assert.Equal(gilton.Id, alerta.DestinatarioUsuarioId);
        Assert.Equal(obra.Id, alerta.ObraId);
        Assert.Equal(SeveridadeAlerta.Critico, alerta.Severidade);
        Assert.Equal(StatusAlerta.Aberto, alerta.Status);
        Assert.StartsWith("VENCIDO", alerta.Titulo);
        Assert.Contains("AAA1A11", alerta.Titulo);

        // Nova inspeção concluída hoje empurra o vencimento: o próximo ciclo encerra o alerta.
        db.Add(new Inspecao
        {
            ObraId = obra.Id, TipoInspecao = TipoInspecao.Veiculo, VeiculoId = veiculo.Id, Status = StatusInspecao.Concluida,
            Data = DateTime.UtcNow, ChecklistModeloId = Guid.NewGuid(), ResponsavelUsuarioId = gilton.Id,
        });
        await db.SaveChangesAsync();
        await motor.ProcessarAsync();

        Assert.Equal(StatusAlerta.Resolvido, (await db.Alertas.SingleAsync()).Status);
    }
}
