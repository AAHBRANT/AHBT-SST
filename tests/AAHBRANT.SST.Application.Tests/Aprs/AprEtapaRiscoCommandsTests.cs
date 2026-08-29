using AAHBRANT.SST.Application.AprEtapaRiscos.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Aprs;

public class AprEtapaRiscoCommandsTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<Guid> SemearEtapaAsync(IAppDbContext db)
    {
        var atividade = new Atividade { Nome = "Escavação de vala" };
        db.Atividades.Add(atividade);
        var apr = new Apr { AtividadeId = atividade.Id, Local = "Frente 3", Data = DateTime.UtcNow };
        db.Aprs.Add(apr);
        var etapa = new AprEtapa { AprId = apr.Id, Ordem = 1, Descricao = "Escavação de vala" };
        db.AprEtapas.Add(etapa);
        await db.SaveChangesAsync();
        return etapa.Id;
    }

    [Fact]
    public async Task Criar_CalculaNivelRiscoInicialEResidualCorretamente()
    {
        var db = CriarDb(nameof(Criar_CalculaNivelRiscoInicialEResidualCorretamente));
        var etapaId = await SemearEtapaAsync(db);
        var handler = new CriarAprEtapaRiscoCommandHandler(db);

        var id = await handler.Handle(new CriarAprEtapaRiscoCommand(
            etapaId,
            "Desabamento / soterramento",
            "Instabilidade do solo; proteção inadequada",
            "Asfixia, esmagamento, óbito",
            "Trabalhadores na escavação",
            3, 5,
            "Taludamento/escoramento conforme condição; afastar cargas da borda",
            "Engenharia / SST / Encarregado",
            1, 5), default);

        var risco = await db.AprEtapaRiscos.SingleAsync(r => r.Id == id);
        Assert.Equal(NivelRiscoApr.Alto, risco.NivelRiscoInicial); // 3*5=15
        Assert.Equal(NivelRiscoApr.Moderado, risco.NivelRiscoResidual); // 1*5=5
    }

    [Fact]
    public async Task Criar_EtapaInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Criar_EtapaInexistente_LancaKeyNotFoundException));
        var handler = new CriarAprEtapaRiscoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(new CriarAprEtapaRiscoCommand(
            Guid.NewGuid(), "Perigo", null, null, null, 1, 1, null, null, 1, 1), default));
    }

    [Fact]
    public async Task Atualizar_RecalculaNiveisDeRisco()
    {
        var db = CriarDb(nameof(Atualizar_RecalculaNiveisDeRisco));
        var etapaId = await SemearEtapaAsync(db);
        var id = await new CriarAprEtapaRiscoCommandHandler(db).Handle(new CriarAprEtapaRiscoCommand(
            etapaId, "Perigo original", null, null, null, 1, 1, null, null, 1, 1), default);

        await new AtualizarAprEtapaRiscoCommandHandler(db).Handle(new AtualizarAprEtapaRiscoCommand(
            id, "Perigo atualizado", "Fonte", "Lesões", "Expostos", 5, 5, "Medidas", "Responsável", 1, 4), default);

        var risco = await db.AprEtapaRiscos.SingleAsync(r => r.Id == id);
        Assert.Equal("Perigo atualizado", risco.PerigoEventoPerigoso);
        Assert.Equal(NivelRiscoApr.Critico, risco.NivelRiscoInicial); // 5*5=25
        Assert.Equal(NivelRiscoApr.Baixo, risco.NivelRiscoResidual); // 1*4=4
    }

    [Fact]
    public async Task Excluir_RemoveORiscoDaEtapa()
    {
        var db = CriarDb(nameof(Excluir_RemoveORiscoDaEtapa));
        var etapaId = await SemearEtapaAsync(db);
        var id = await new CriarAprEtapaRiscoCommandHandler(db).Handle(new CriarAprEtapaRiscoCommand(
            etapaId, "Perigo", null, null, null, 1, 1, null, null, 1, 1), default);

        await new ExcluirAprEtapaRiscoCommandHandler(db).Handle(new ExcluirAprEtapaRiscoCommand(id), default);

        Assert.False(await db.AprEtapaRiscos.AnyAsync(r => r.Id == id));
    }
}
