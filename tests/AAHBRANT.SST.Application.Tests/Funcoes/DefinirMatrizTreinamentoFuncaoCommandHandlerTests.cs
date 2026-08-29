using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Commands;
using AAHBRANT.SST.Application.Funcoes.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

// Mesmo padrão de DefinirMatrizEpiFuncaoCommandHandlerTests — a matriz de treinamento é o mesmo
// mecanismo (replace-all idempotente), só trocando CatalogoEpi por CursoTreinamento.
public class DefinirMatrizTreinamentoFuncaoCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Funcao Funcao, CursoTreinamento CursoA, CursoTreinamento CursoB)> SemearAsync(IAppDbContext db)
    {
        var funcao = new Funcao { Nome = "Montador de andaime" };
        var cursoA = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", CargaHorariaMinima = 8, ValidadeEmMeses = 24 };
        var cursoB = new CursoTreinamento { Nome = "NR-18 Andaimes", CargaHorariaMinima = 4, ValidadeEmMeses = 12 };

        db.Funcoes.Add(funcao);
        db.CursosTreinamento.AddRange(cursoA, cursoB);
        await db.SaveChangesAsync();

        return (funcao, cursoA, cursoB);
    }

    [Fact]
    public async Task Handle_FuncaoSemVinculos_AdicionaTodosOsCursosInformados()
    {
        var db = CriarDb(nameof(Handle_FuncaoSemVinculos_AdicionaTodosOsCursosInformados));
        var (funcao, cursoA, cursoB) = await SemearAsync(db);
        var handler = new DefinirMatrizTreinamentoFuncaoCommandHandler(db);

        await handler.Handle(new DefinirMatrizTreinamentoFuncaoCommand(funcao.Id, new List<Guid> { cursoA.Id, cursoB.Id }), default);

        var vinculos = await db.MatrizTreinamentoFuncoes.Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
    }

    [Fact]
    public async Task Handle_RemoveCursoDaLista_DesativaVinculoExistente()
    {
        var db = CriarDb(nameof(Handle_RemoveCursoDaLista_DesativaVinculoExistente));
        var (funcao, cursoA, cursoB) = await SemearAsync(db);
        var handler = new DefinirMatrizTreinamentoFuncaoCommandHandler(db);
        await handler.Handle(new DefinirMatrizTreinamentoFuncaoCommand(funcao.Id, new List<Guid> { cursoA.Id, cursoB.Id }), default);

        await handler.Handle(new DefinirMatrizTreinamentoFuncaoCommand(funcao.Id, new List<Guid> { cursoA.Id }), default);

        var vinculos = await db.MatrizTreinamentoFuncoes.IgnoreQueryFilters().Where(m => m.FuncaoId == funcao.Id).ToListAsync();
        Assert.Equal(2, vinculos.Count);
        Assert.True(vinculos.Single(v => v.CursoTreinamentoId == cursoA.Id).Ativo);
        Assert.False(vinculos.Single(v => v.CursoTreinamentoId == cursoB.Id).Ativo);
    }

    [Fact]
    public async Task Handle_FuncaoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_FuncaoInexistente_LancaKeyNotFoundException));
        var handler = new DefinirMatrizTreinamentoFuncaoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new DefinirMatrizTreinamentoFuncaoCommand(Guid.NewGuid(), new List<Guid>()), default));
    }

    [Fact]
    public async Task ListarTreinamentosObrigatoriosPorFuncao_RetornaCursosVinculados()
    {
        var db = CriarDb(nameof(ListarTreinamentosObrigatoriosPorFuncao_RetornaCursosVinculados));
        var (funcao, cursoA, _) = await SemearAsync(db);
        await new DefinirMatrizTreinamentoFuncaoCommandHandler(db).Handle(
            new DefinirMatrizTreinamentoFuncaoCommand(funcao.Id, new List<Guid> { cursoA.Id }), default);

        var lista = await new ListarTreinamentosObrigatoriosPorFuncaoQueryHandler(db)
            .Handle(new ListarTreinamentosObrigatoriosPorFuncaoQuery(funcao.Id), default);

        var curso = Assert.Single(lista);
        Assert.Equal(cursoA.Id, curso.Id);
    }
}
