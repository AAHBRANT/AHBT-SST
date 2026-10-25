using AAHBRANT.SST.Application.AcoesPlano.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.AcoesPlano;

// Ocorrência do CAC (24/10/2026): o técnico concluiu o plano inteiro à mão. O botão reabre o que foi
// concluído sem o fato real e deixa em paz o que foi concluído pelo fato.
public class ReabrirAcoesSemExecucaoTests
{
    [Fact]
    public async Task Reabre_ManuaisESemFatoReal_EMantemOQueTemFatoReal()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "CAC" };
        var tecnico = new Usuario { Email = "g@x.com", Nome = "Gilton" };
        var acidente = new Acidente { Tipo = TipoOcorrencia.QuaseAcidente, ObraId = obra.Id, Local = "Bloco B", Data = new DateTime(2026, 10, 21), Descricao = "Peça caiu" };
        AcaoPlano Acao(string d, TipoResolucaoAcaoPlano? tipo = null, TipoInspecao? insp = null) => new()
        {
            OrigemTipo = nameof(Acidente), OrigemId = acidente.Id, Tipo = TipoAcaoPlano.Corretiva, Descricao = d, Prioridade = PrioridadeAcao.Alta,
            Status = StatusControleRisco.Concluido, DataConclusao = DateTime.UtcNow, ConcluidoPorUsuarioId = tecnico.Id, DescricaoConclusao = "feito",
            TipoResolucao = tipo, ResolucaoTipoInspecao = insp, ResponsavelUsuarioId = tecnico.Id,
        };
        var manual = Acao("manual");
        var inspecaoNaoFeita = Acao("inspeção", TipoResolucaoAcaoPlano.Inspecao, TipoInspecao.Andaimes);
        var ddsFuturo = Acao("dds futuro");
        var ddsFeito = Acao("dds feito");
        var reuniaoNaoFeita = Acao("reunião");
        db.AddRange(obra, tecnico, acidente, manual, inspecaoNaoFeita, ddsFuturo, ddsFeito, reuniaoNaoFeita);
        db.AddRange(
            new TemaDdsAgendado { ObraId = obra.Id, Data = new DateTime(2026, 10, 26), CatalogoTemaDdsId = Guid.NewGuid(), OrigemTipo = nameof(Acidente), OrigemId = acidente.Id, AcaoPlanoId = ddsFuturo.Id },
            new TemaDdsAgendado { ObraId = obra.Id, Data = new DateTime(2026, 10, 22), CatalogoTemaDdsId = Guid.NewGuid(), OrigemTipo = nameof(Acidente), OrigemId = acidente.Id, AcaoPlanoId = ddsFeito.Id, AplicadoEm = DateTime.UtcNow },
            new ReuniaoAnaliseOcorrencia { AcidenteId = acidente.Id, Inicio = DateTime.UtcNow, Fim = DateTime.UtcNow.AddHours(1), Participantes = "x", AcaoPlanoId = reuniaoNaoFeita.Id });
        await db.SaveChangesAsync();

        var reabertas = await new ReabrirAcoesSemExecucaoCommandHandler(db).Handle(new ReabrirAcoesSemExecucaoCommand(acidente.Id), default);

        Assert.Equal(4, reabertas);
        var salvas = await db.AcoesPlano.ToDictionaryAsync(a => a.Descricao);
        foreach (var d in new[] { "manual", "inspeção", "dds futuro", "reunião" })
        {
            Assert.Equal(StatusControleRisco.Pendente, salvas[d].Status);
            Assert.Null(salvas[d].DataConclusao);
            Assert.Null(salvas[d].ConcluidoPorUsuarioId);
        }
        Assert.Equal(StatusControleRisco.Concluido, salvas["dds feito"].Status);
    }

    [Fact]
    public async Task InvestigacaoConcluida_Recusa()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "CAC" };
        var acidente = new Acidente { Tipo = TipoOcorrencia.QuaseAcidente, ObraId = obra.Id, Local = "B", Data = DateTime.UtcNow, Descricao = "d", Status = StatusAcidente.Concluido };
        db.AddRange(obra, acidente);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ReabrirAcoesSemExecucaoCommandHandler(db).Handle(new ReabrirAcoesSemExecucaoCommand(acidente.Id), default));
    }
}
