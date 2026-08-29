using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.QuestionarioAplicabilidade.Commands;
using AAHBRANT.SST.Application.QuestionarioAplicabilidade.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.QuestionarioAplicabilidade;

public class QuestionarioAplicabilidadeCommandsTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Criar_Atualizar_Excluir_Item_FuncionaCorretamente()
    {
        var db = CriarDb(nameof(Criar_Atualizar_Excluir_Item_FuncionaCorretamente));
        var id = await new CriarItemQuestionarioAplicabilidadeCommandHandler(db)
            .Handle(new CriarItemQuestionarioAplicabilidadeCommand("A obra tem espaço confinado?", null), default);

        await new AtualizarItemQuestionarioAplicabilidadeCommandHandler(db).Handle(
            new AtualizarItemQuestionarioAplicabilidadeCommand(id, "A obra possui espaço confinado?", "Ver NR-33"), default);
        var atualizado = await db.ItensQuestionarioAplicabilidade.SingleAsync(i => i.Id == id);
        Assert.Equal("A obra possui espaço confinado?", atualizado.Pergunta);

        await new ExcluirItemQuestionarioAplicabilidadeCommandHandler(db).Handle(new ExcluirItemQuestionarioAplicabilidadeCommand(id), default);
        var lista = await new ListarItensQuestionarioAplicabilidadeQueryHandler(db).Handle(new ListarItensQuestionarioAplicabilidadeQuery(), default);
        Assert.DoesNotContain(lista, i => i.Id == id);
    }

    [Fact]
    public async Task Responder_ObraNova_CriaResposta()
    {
        var db = CriarDb(nameof(Responder_ObraNova_CriaResposta));
        var obra = new Obra { Codigo = "OBR-1", Nome = "Obra 1" };
        db.Obras.Add(obra);
        var item = new ItemQuestionarioAplicabilidade { Pergunta = "Pergunta?" };
        db.ItensQuestionarioAplicabilidade.Add(item);
        await db.SaveChangesAsync();
        var handler = new ResponderQuestionarioAplicabilidadeCommandHandler(db);

        await handler.Handle(new ResponderQuestionarioAplicabilidadeCommand(obra.Id, item.Id, true, "obs"), default);

        var resposta = await db.RespostasQuestionarioAplicabilidade.SingleAsync(r => r.ObraId == obra.Id);
        Assert.True(resposta.Resposta);
        Assert.Equal("obs", resposta.Observacao);
    }

    [Fact]
    public async Task Responder_DeNovo_AtualizaEmVezDeDuplicar()
    {
        var db = CriarDb(nameof(Responder_DeNovo_AtualizaEmVezDeDuplicar));
        var obra = new Obra { Codigo = "OBR-2", Nome = "Obra 2" };
        db.Obras.Add(obra);
        var item = new ItemQuestionarioAplicabilidade { Pergunta = "Pergunta?" };
        db.ItensQuestionarioAplicabilidade.Add(item);
        await db.SaveChangesAsync();
        var handler = new ResponderQuestionarioAplicabilidadeCommandHandler(db);
        await handler.Handle(new ResponderQuestionarioAplicabilidadeCommand(obra.Id, item.Id, true, null), default);

        await handler.Handle(new ResponderQuestionarioAplicabilidadeCommand(obra.Id, item.Id, false, "mudou"), default);

        var respostas = await db.RespostasQuestionarioAplicabilidade.Where(r => r.ObraId == obra.Id).ToListAsync();
        var unica = Assert.Single(respostas);
        Assert.False(unica.Resposta);
        Assert.Equal("mudou", unica.Observacao);
    }

    [Fact]
    public async Task ObterQuestionarioObra_ItemSemResposta_RetornaRespostaNull()
    {
        var db = CriarDb(nameof(ObterQuestionarioObra_ItemSemResposta_RetornaRespostaNull));
        var obra = new Obra { Codigo = "OBR-3", Nome = "Obra 3" };
        db.Obras.Add(obra);
        var item = new ItemQuestionarioAplicabilidade { Pergunta = "Pergunta ainda não respondida?" };
        db.ItensQuestionarioAplicabilidade.Add(item);
        await db.SaveChangesAsync();

        var resultado = await new ObterQuestionarioAplicabilidadeObraQueryHandler(db)
            .Handle(new ObterQuestionarioAplicabilidadeObraQuery(obra.Id), default);

        var linha = Assert.Single(resultado);
        Assert.Null(linha.Resposta);
    }
}
