using AAHBRANT.SST.Application.AcoesPlano.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.AcoesPlano;

// Concluir com descrição + Reabrir (23/10/2026): técnico "validou" o plano inteiro por engano.
public class ConcluirReabrirAcaoPlanoTests
{
    [Fact]
    public async Task Concluir_Manual_RegistraQuemEOQueFoiFeito()
    {
        var (db, acao, tecnico, _) = await CenarioAsync();

        await new ConcluirAcaoPlanoCommandHandler(db).Handle(new ConcluirAcaoPlanoCommand(acao.Id, "  Rodapés instalados.  ", tecnico.Id), default);

        var salva = await db.AcoesPlano.SingleAsync(a => a.Id == acao.Id);
        Assert.Equal(StatusControleRisco.Concluido, salva.Status);
        Assert.NotNull(salva.DataConclusao);
        Assert.Equal(tecnico.Id, salva.ConcluidoPorUsuarioId);
        Assert.Equal("Rodapés instalados.", salva.DescricaoConclusao);
    }

    [Fact]
    public async Task Concluir_AcaoDoDdsAutomatico_Recusa()
    {
        var (db, acao, tecnico, acidente) = await CenarioAsync();
        db.TemasDdsAgendados.Add(new TemaDdsAgendado
        {
            ObraId = acidente.ObraId, Data = new DateTime(2026, 10, 24), CatalogoTemaDdsId = Guid.NewGuid(),
            OrigemTipo = nameof(Acidente), OrigemId = acidente.Id, AcaoPlanoId = acao.Id,
        });
        await db.SaveChangesAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ConcluirAcaoPlanoCommandHandler(db).Handle(new ConcluirAcaoPlanoCommand(acao.Id, "feito", tecnico.Id), default));
        Assert.Contains("automaticamente", erro.Message);
    }

    [Fact]
    public async Task Reabrir_AcaoValidadaPorEngano_VoltaParaPendenteSemValidacao()
    {
        var (db, acao, tecnico, _) = await CenarioAsync();
        acao.Status = StatusControleRisco.Concluido;
        acao.DataConclusao = DateTime.UtcNow;
        acao.DataValidacao = DateTime.UtcNow;
        acao.ValidadoPorUsuarioId = tecnico.Id;
        await db.SaveChangesAsync();

        await new ReabrirAcaoPlanoCommandHandler(db).Handle(new ReabrirAcaoPlanoCommand(acao.Id), default);

        var salva = await db.AcoesPlano.SingleAsync(a => a.Id == acao.Id);
        Assert.Equal(StatusControleRisco.Pendente, salva.Status);
        Assert.Null(salva.DataConclusao);
        Assert.Null(salva.DataValidacao);
        Assert.Null(salva.ValidadoPorUsuarioId);
    }

    [Fact]
    public async Task Reabrir_InvestigacaoConcluida_Recusa()
    {
        var (db, acao, _, acidente) = await CenarioAsync();
        acao.Status = StatusControleRisco.Concluido;
        acidente.Status = StatusAcidente.Concluido;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ReabrirAcaoPlanoCommandHandler(db).Handle(new ReabrirAcaoPlanoCommand(acao.Id), default));
    }

    [Fact]
    public void Validador_SemDescricao_Recusa()
        => Assert.False(new ConcluirAcaoPlanoCommandValidator().Validate(new ConcluirAcaoPlanoCommand(Guid.NewGuid(), " ", null)).IsValid);

    private static async Task<(IAppDbContext Db, AcaoPlano Acao, Usuario Tecnico, Acidente Acidente)> CenarioAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "CAC" };
        var tecnico = new Usuario { Nome = "Técnico", Email = "tec@x.com" };
        var acidente = new Acidente { Tipo = TipoOcorrencia.QuaseAcidente, ObraId = obra.Id, Local = "Bloco B", Data = new DateTime(2026, 10, 21), Descricao = "Peça caiu" };
        var acao = new AcaoPlano { OrigemTipo = nameof(Acidente), OrigemId = acidente.Id, Tipo = TipoAcaoPlano.Corretiva, Descricao = "Instalar rodapé", Prioridade = PrioridadeAcao.Alta };
        db.AddRange(obra, tecnico, acidente, acao);
        await db.SaveChangesAsync();
        return (db, acao, tecnico, acidente);
    }
}
