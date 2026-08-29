using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.RequisitosLegais.Commands;
using AAHBRANT.SST.Application.RequisitosLegais.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.RequisitosLegais;

public class RequisitoLegalCommandsTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Criar_PersisteRequisitoComCategoriaEStatusAtivo()
    {
        var db = CriarDb(nameof(Criar_PersisteRequisitoComCategoriaEStatusAtivo));
        var handler = new CriarRequisitoLegalCommandHandler(db);

        var id = await handler.Handle(
            new CriarRequisitoLegalCommand("NR-35", "35.4", "Treinamento em altura", "Descrição", CategoriaRequisitoLegal.Treinamento, "https://..."),
            default);

        var requisito = await db.RequisitosLegais.SingleAsync(r => r.Id == id);
        Assert.Equal("NR-35", requisito.Norma);
        Assert.Equal(StatusRequisitoLegal.Ativo, requisito.Status);
    }

    [Fact]
    public async Task Atualizar_RequisitoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Atualizar_RequisitoInexistente_LancaKeyNotFoundException));
        var handler = new AtualizarRequisitoLegalCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new AtualizarRequisitoLegalCommand(Guid.NewGuid(), "NR-35", null, "x", "y", CategoriaRequisitoLegal.Epi, StatusRequisitoLegal.Ativo, null),
            default));
    }

    [Fact]
    public async Task Excluir_RequisitoDesaparecDaListagemAtiva()
    {
        var db = CriarDb(nameof(Excluir_RequisitoDesaparecDaListagemAtiva));
        var id = await new CriarRequisitoLegalCommandHandler(db).Handle(
            new CriarRequisitoLegalCommand("NR-06", null, "EPI", "Descrição", CategoriaRequisitoLegal.Epi, null), default);

        await new ExcluirRequisitoLegalCommandHandler(db).Handle(new ExcluirRequisitoLegalCommand(id), default);

        var lista = await new ListarRequisitosLegaisQueryHandler(db).Handle(new ListarRequisitosLegaisQuery(null, null), default);
        Assert.DoesNotContain(lista, r => r.Id == id);
    }

    private static async Task<(Guid RequisitoId, Guid PerigoId, Guid FuncaoId, Guid ItemId)> SemearAsync(IAppDbContext db)
    {
        var requisitoId = await new CriarRequisitoLegalCommandHandler(db).Handle(
            new CriarRequisitoLegalCommand("NR-35", "35.4", "Trabalho em altura", "Descrição", CategoriaRequisitoLegal.Treinamento, null), default);

        var perigo = new Perigo { Nome = "Queda de altura" };
        var funcao = new Funcao { Nome = "Montador" };
        var item = new ItemQuestionarioAplicabilidade { Pergunta = "A obra realiza trabalho em altura?" };
        db.Perigos.Add(perigo);
        db.Funcoes.Add(funcao);
        db.ItensQuestionarioAplicabilidade.Add(item);
        await db.SaveChangesAsync();

        return (requisitoId, perigo.Id, funcao.Id, item.Id);
    }

    [Fact]
    public async Task DefinirCriterios_AdicionaCriteriosDeTiposDiferentes()
    {
        var db = CriarDb(nameof(DefinirCriterios_AdicionaCriteriosDeTiposDiferentes));
        var (requisitoId, perigoId, funcaoId, itemId) = await SemearAsync(db);
        var handler = new DefinirCriteriosRequisitoLegalCommandHandler(db);

        await handler.Handle(new DefinirCriteriosRequisitoLegalCommand(requisitoId, new List<CriterioAplicabilidadeInput>
        {
            new(TipoCriterioAplicabilidade.Perigo, perigoId, null, null, null),
            new(TipoCriterioAplicabilidade.Funcao, null, funcaoId, null, null),
            new(TipoCriterioAplicabilidade.Equipamento, null, null, TipoAtivo.Equipamento, null),
            new(TipoCriterioAplicabilidade.ItemQuestionario, null, null, null, itemId),
        }), default);

        var criterios = await db.RequisitoLegalCriterios.Where(c => c.RequisitoLegalId == requisitoId).ToListAsync();
        Assert.Equal(4, criterios.Count);
    }

    [Fact]
    public async Task DefinirCriterios_RemoveCriterioDaListaDesativaSemExcluir()
    {
        var db = CriarDb(nameof(DefinirCriterios_RemoveCriterioDaListaDesativaSemExcluir));
        var (requisitoId, perigoId, funcaoId, _) = await SemearAsync(db);
        var handler = new DefinirCriteriosRequisitoLegalCommandHandler(db);
        await handler.Handle(new DefinirCriteriosRequisitoLegalCommand(requisitoId, new List<CriterioAplicabilidadeInput>
        {
            new(TipoCriterioAplicabilidade.Perigo, perigoId, null, null, null),
            new(TipoCriterioAplicabilidade.Funcao, null, funcaoId, null, null),
        }), default);

        await handler.Handle(new DefinirCriteriosRequisitoLegalCommand(requisitoId, new List<CriterioAplicabilidadeInput>
        {
            new(TipoCriterioAplicabilidade.Perigo, perigoId, null, null, null),
        }), default);

        var todos = await db.RequisitoLegalCriterios.IgnoreQueryFilters()
            .Where(c => c.RequisitoLegalId == requisitoId).ToListAsync();
        Assert.Equal(2, todos.Count);
        Assert.True(todos.Single(c => c.Tipo == TipoCriterioAplicabilidade.Perigo).Ativo);
        Assert.False(todos.Single(c => c.Tipo == TipoCriterioAplicabilidade.Funcao).Ativo);
    }

    [Fact]
    public async Task DefinirCriterios_ReenviaCriterioRemovidoAnteriormente_ReativaEmVezDeDuplicar()
    {
        var db = CriarDb(nameof(DefinirCriterios_ReenviaCriterioRemovidoAnteriormente_ReativaEmVezDeDuplicar));
        var (requisitoId, perigoId, funcaoId, _) = await SemearAsync(db);
        var handler = new DefinirCriteriosRequisitoLegalCommandHandler(db);
        var comAmbos = new List<CriterioAplicabilidadeInput>
        {
            new(TipoCriterioAplicabilidade.Perigo, perigoId, null, null, null),
            new(TipoCriterioAplicabilidade.Funcao, null, funcaoId, null, null),
        };
        await handler.Handle(new DefinirCriteriosRequisitoLegalCommand(requisitoId, comAmbos), default);
        await handler.Handle(new DefinirCriteriosRequisitoLegalCommand(requisitoId,
            new List<CriterioAplicabilidadeInput> { comAmbos[0] }), default);

        await handler.Handle(new DefinirCriteriosRequisitoLegalCommand(requisitoId, comAmbos), default);

        var todos = await db.RequisitoLegalCriterios.Where(c => c.RequisitoLegalId == requisitoId).ToListAsync();
        Assert.Equal(2, todos.Count);
        Assert.All(todos, c => Assert.True(c.Ativo));
    }

    [Fact]
    public async Task DefinirCriterios_PerigoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(DefinirCriterios_PerigoInexistente_LancaKeyNotFoundException));
        var (requisitoId, _, _, _) = await SemearAsync(db);
        var handler = new DefinirCriteriosRequisitoLegalCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new DefinirCriteriosRequisitoLegalCommand(requisitoId,
                new List<CriterioAplicabilidadeInput> { new(TipoCriterioAplicabilidade.Perigo, Guid.NewGuid(), null, null, null) }),
            default));
    }

    [Fact]
    public async Task ObterDetalhe_RetornaCriteriosComNomesResolvidos()
    {
        var db = CriarDb(nameof(ObterDetalhe_RetornaCriteriosComNomesResolvidos));
        var (requisitoId, perigoId, _, _) = await SemearAsync(db);
        await new DefinirCriteriosRequisitoLegalCommandHandler(db).Handle(
            new DefinirCriteriosRequisitoLegalCommand(requisitoId,
                new List<CriterioAplicabilidadeInput> { new(TipoCriterioAplicabilidade.Perigo, perigoId, null, null, null) }),
            default);

        var detalhe = await new ObterRequisitoLegalDetalheQueryHandler(db).Handle(new ObterRequisitoLegalDetalheQuery(requisitoId), default);

        Assert.NotNull(detalhe);
        var criterio = Assert.Single(detalhe!.Criterios);
        Assert.Equal("Queda de altura", criterio.PerigoNome);
    }
}
