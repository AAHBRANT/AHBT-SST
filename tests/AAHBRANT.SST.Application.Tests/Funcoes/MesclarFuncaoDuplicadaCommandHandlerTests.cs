using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

public class MesclarFuncaoDuplicadaCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_ReatribuiTrabalhadoresDaDuplicataSemCboParaAComCbo()
    {
        var db = CriarDb(nameof(Handle_ReatribuiTrabalhadoresDaDuplicataSemCboParaAComCbo));
        var manter = new Funcao { Nome = "Técnico de Segurança do Trabalho", CboCodigo = "3516-05" };
        var remover = new Funcao { Nome = "Técnico de Segurança do Trabalho", CboCodigo = null };
        db.Funcoes.AddRange(manter, remover);
        var obra = new Obra { Nome = "Obra Teste", Codigo = "OBRA-1" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var trabalhador = new Trabalhador { Nome = "Gabriel", ObraId = obra.Id, FuncaoId = remover.Id };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var handler = new MesclarFuncaoDuplicadaCommandHandler(db);
        await handler.Handle(new MesclarFuncaoDuplicadaCommand(manter.Id, remover.Id), default);

        var trabalhadorAtualizado = await db.Trabalhadores.IgnoreQueryFilters().SingleAsync(t => t.Id == trabalhador.Id);
        Assert.Equal(manter.Id, trabalhadorAtualizado.FuncaoId);
    }

    [Fact]
    public async Task Handle_ExcluiFuncaoDuplicadaAoFinal()
    {
        // "Excluir" neste app é sempre soft-delete (SstDbContext.AplicarAuditoria converte todo
        // EntityState.Deleted em Ativo=false) — a linha continua existindo, só sai das buscas normais
        // por causa do HasQueryFilter(f => f.Ativo) da configuração de Funcao.
        var db = CriarDb(nameof(Handle_ExcluiFuncaoDuplicadaAoFinal));
        var manter = new Funcao { Nome = "Encarregado", CboCodigo = "7102-05" };
        var remover = new Funcao { Nome = "Encarregado", CboCodigo = null };
        db.Funcoes.AddRange(manter, remover);
        await db.SaveChangesAsync();

        var handler = new MesclarFuncaoDuplicadaCommandHandler(db);
        await handler.Handle(new MesclarFuncaoDuplicadaCommand(manter.Id, remover.Id), default);

        Assert.False(await db.Funcoes.AnyAsync(f => f.Id == remover.Id));
        var removerAtualizado = await db.Funcoes.IgnoreQueryFilters().SingleAsync(f => f.Id == remover.Id);
        Assert.False(removerAtualizado.Ativo);
        Assert.True(await db.Funcoes.AnyAsync(f => f.Id == manter.Id));
    }

    [Fact]
    public async Task Handle_MatrizEpiSemConflito_ReatribuiParaAFuncaoMantida()
    {
        var db = CriarDb(nameof(Handle_MatrizEpiSemConflito_ReatribuiParaAFuncaoMantida));
        var manter = new Funcao { Nome = "Soldador", CboCodigo = "7243-05" };
        var remover = new Funcao { Nome = "Soldador", CboCodigo = null };
        var epi = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        db.Funcoes.AddRange(manter, remover);
        db.CatalogoEpis.Add(epi);
        await db.SaveChangesAsync();

        db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao { FuncaoId = remover.Id, CatalogoEpiId = epi.Id });
        await db.SaveChangesAsync();

        var handler = new MesclarFuncaoDuplicadaCommandHandler(db);
        await handler.Handle(new MesclarFuncaoDuplicadaCommand(manter.Id, remover.Id), default);

        var vinculo = await db.MatrizEpiFuncoes.IgnoreQueryFilters().SingleAsync(m => m.CatalogoEpiId == epi.Id);
        Assert.Equal(manter.Id, vinculo.FuncaoId);
    }

    [Fact]
    public async Task Handle_MatrizEpiJaExisteNaFuncaoMantida_DescartaARedundanteEmVezDeViolarIndiceUnico()
    {
        var db = CriarDb(nameof(Handle_MatrizEpiJaExisteNaFuncaoMantida_DescartaARedundanteEmVezDeViolarIndiceUnico));
        var manter = new Funcao { Nome = "Soldador", CboCodigo = "7243-05" };
        var remover = new Funcao { Nome = "Soldador", CboCodigo = null };
        var epi = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        db.Funcoes.AddRange(manter, remover);
        db.CatalogoEpis.Add(epi);
        await db.SaveChangesAsync();

        db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao { FuncaoId = manter.Id, CatalogoEpiId = epi.Id });
        db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao { FuncaoId = remover.Id, CatalogoEpiId = epi.Id });
        await db.SaveChangesAsync();

        var handler = new MesclarFuncaoDuplicadaCommandHandler(db);
        await handler.Handle(new MesclarFuncaoDuplicadaCommand(manter.Id, remover.Id), default);

        // A redundante vira soft-delete (Ativo=false, ver SstDbContext.AplicarAuditoria) em vez de
        // sumir de vez — só a linha ATIVA é que precisa ser única para (FuncaoId, CatalogoEpiId).
        var vinculosAtivos = await db.MatrizEpiFuncoes.Where(m => m.CatalogoEpiId == epi.Id).ToListAsync();
        Assert.Single(vinculosAtivos);
        Assert.Equal(manter.Id, vinculosAtivos[0].FuncaoId);
    }

    [Fact]
    public async Task Handle_FuncaoAManterSemCbo_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_FuncaoAManterSemCbo_LancaInvalidOperationException));
        var manter = new Funcao { Nome = "Pedreiro", CboCodigo = null };
        var remover = new Funcao { Nome = "Pedreiro", CboCodigo = null };
        db.Funcoes.AddRange(manter, remover);
        await db.SaveChangesAsync();

        var handler = new MesclarFuncaoDuplicadaCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new MesclarFuncaoDuplicadaCommand(manter.Id, remover.Id), default));
    }

    [Fact]
    public async Task Handle_NomesDiferentes_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_NomesDiferentes_LancaInvalidOperationException));
        var manter = new Funcao { Nome = "Encarregado", CboCodigo = "7102-05" };
        var remover = new Funcao { Nome = "Auxiliar de Encarregado", CboCodigo = null };
        db.Funcoes.AddRange(manter, remover);
        await db.SaveChangesAsync();

        var handler = new MesclarFuncaoDuplicadaCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new MesclarFuncaoDuplicadaCommand(manter.Id, remover.Id), default));
    }
}
