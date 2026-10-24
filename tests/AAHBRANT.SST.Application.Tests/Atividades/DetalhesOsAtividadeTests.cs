using AAHBRANT.SST.Application.Atividades.DetalhesOs;
using AAHBRANT.SST.Application.MateriaisApoio.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Atividades;

// Etapa 2 da Ordem de Serviço (20/10/2026): documentos controlados e detalhes da atividade.
public class DetalhesOsAtividadeTests
{
    private sealed class AnalistaFalso : IAnalistaItensAtividadeOs
    {
        private readonly ItemSugeridoIa[] _itens;
        public AnalistaFalso(params ItemSugeridoIa[] itens) => _itens = itens;
        public Task<IReadOnlyList<ItemSugeridoIa>> SugerirAsync(ContextoItensAtividadeOs contexto, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ItemSugeridoIa>>(_itens);
    }

    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, Atividade Atividade)> CriarAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB-01", Nome = "Obra Teste" };
        var ghe = new Ghe { ObraId = obra.Id, Numero = 3, DescricaoAmbiente = "Laje a céu aberto", JornadaTrabalho = "07h às 17h" };
        var funcao = new Funcao { Nome = "ARMADOR" };
        var curso = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", NormaReferencia = "NR-35" };
        var atividade = new Atividade { ObraId = obra.Id, GheId = ghe.Id, Nome = "Montagem de armadura" };
        db.AddRange(obra, ghe, funcao, curso, atividade,
            new GheFuncao { GheId = ghe.Id, FuncaoId = funcao.Id },
            new MatrizTreinamentoFuncao { FuncaoId = funcao.Id, CursoTreinamentoId = curso.Id });
        await db.SaveChangesAsync();
        return (db, atividade);
    }

    [Fact]
    public async Task Obter_SemCondicoesProprias_UsaGheEMostraTreinamentosDaMatriz()
    {
        var (db, atividade) = await CriarAsync();

        var dto = await new ObterDetalhesOsAtividadeQueryHandler(db).Handle(new ObterDetalhesOsAtividadeQuery(atividade.Id), default);

        Assert.True(dto!.CondicoesDoPgr);
        Assert.Contains("Laje a céu aberto", dto.Condicoes);
        Assert.Contains("07h às 17h", dto.Condicoes);
        var t = Assert.Single(dto.Treinamentos);
        Assert.Equal("NR-35 Trabalho em Altura", t.Curso);
        Assert.Equal("ARMADOR", t.Funcao);
    }

    [Fact]
    public async Task Salvar_VinculaProcedimentoVigente_ERecusaEmRevisao()
    {
        var (db, atividade) = await CriarAsync();
        var vigente = new MaterialApoio { Nome = "Trabalho em altura", NomeArquivo = "a.pdf", ContentType = "application/pdf", Tipo = TipoDocumentoSst.Procedimento, Codigo = "PO-1", Revisao = "02", Situacao = SituacaoDocumentoSst.Vigente };
        var emRevisao = new MaterialApoio { Nome = "Escavação", NomeArquivo = "b.pdf", ContentType = "application/pdf", Tipo = TipoDocumentoSst.Procedimento, Codigo = "PO-2", Revisao = "01", Situacao = SituacaoDocumentoSst.EmRevisao };
        var epc = new CatalogoEpc { Nome = "Guarda-corpo" };
        db.AddRange(vigente, emRevisao, epc);
        await db.SaveChangesAsync();
        var handler = new SalvarDetalhesOsAtividadeCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new SalvarDetalhesOsAtividadeCommand(atividade.Id, null, new(), new(), new() { emRevisao.Id }), default));

        await handler.Handle(new SalvarDetalhesOsAtividadeCommand(atividade.Id, " Sobre laje ",
            new() { "Policorte", " policorte ", "", "Torquês" }, new() { epc.Id }, new() { vigente.Id }), default);

        var salva = await db.Atividades.SingleAsync();
        Assert.Equal("Sobre laje", salva.CondicoesExecucao);
        Assert.Equal("Policorte\nTorquês", salva.Ferramentas);
        Assert.Equal(vigente.Id, (await db.AtividadeDocumentos.SingleAsync()).MaterialApoioId);
        Assert.Equal(epc.Id, (await db.AtividadeEpcs.SingleAsync()).CatalogoEpcId);
    }

    [Fact]
    public async Task Sugerir_EntraPendente_EIgnoraRepetido()
    {
        var (db, atividade) = await CriarAsync();
        db.AtividadeItensOs.Add(new AtividadeItemOs { AtividadeId = atividade.Id, Tipo = TipoItemAtividadeOs.InterromperQuando, Texto = "Vento forte ou chuva", Status = StatusMedidaPrevencao.Validada });
        await db.SaveChangesAsync();
        var analista = new AnalistaFalso(
            new ItemSugeridoIa(TipoItemAtividadeOs.InterromperQuando, "vento forte ou chuva.", null),
            new ItemSugeridoIa(TipoItemAtividadeOs.Restricao, "Operar policorte", "Autorização formal"),
            new ItemSugeridoIa(null, "tipo inválido", null));

        var novos = await new SugerirItensAtividadeOsCommandHandler(db, analista).Handle(new SugerirItensAtividadeOsCommand(atividade.Id), default);

        Assert.Equal(1, novos);
        var item = await db.AtividadeItensOs.SingleAsync(i => i.Tipo == TipoItemAtividadeOs.Restricao);
        Assert.Equal(StatusMedidaPrevencao.Pendente, item.Status);
        Assert.Equal(OrigemMedidaPrevencao.SugestaoIa, item.OrigemItem);
        Assert.Equal("Autorização formal", item.Requisito);
    }

    [Fact]
    public async Task Adicionar_RestricaoSemRequisito_EhInvalida_EItemManualNasceValidado()
    {
        var validador = new AdicionarItemAtividadeOsCommandValidator();
        Assert.False(validador.Validate(new AdicionarItemAtividadeOsCommand(Guid.NewGuid(), TipoItemAtividadeOs.Restricao, "Operar policorte", null, null, null)).IsValid);

        var (db, atividade) = await CriarAsync();
        await new AdicionarItemAtividadeOsCommandHandler(db).Handle(
            new AdicionarItemAtividadeOsCommand(atividade.Id, TipoItemAtividadeOs.Autorizacao, "PT emitida", "ignorado", null, "Téc. Ana"), default);
        var item = await db.AtividadeItensOs.SingleAsync();
        Assert.Equal(StatusMedidaPrevencao.Validada, item.Status);
        Assert.Null(item.Requisito);
        Assert.Equal("Téc. Ana", item.ValidadoPorNome);
    }

    [Theory]
    [InlineData(TipoDocumentoSst.Procedimento, null, false, false)]   // controlado sem código
    [InlineData(TipoDocumentoSst.Procedimento, "PO-1", true, false)]   // só política entra em todas as OS
    [InlineData(TipoDocumentoSst.Politica, "POL-1", true, true)]
    [InlineData(TipoDocumentoSst.MaterialApoio, null, false, true)]
    public void CriarDocumento_RegrasDoDocumentoControlado(TipoDocumentoSst tipo, string? codigo, bool todasOs, bool valido)
    {
        var pdf = "%PDF-1.4 teste"u8.ToArray();
        var cmd = new CriarMaterialApoioCommand("Doc", null, "d.pdf", "application/pdf", pdf, tipo, codigo,
            codigo is null ? null : "00", codigo is null ? null : SituacaoDocumentoSst.Vigente, null, todasOs);

        var r = new CriarMaterialApoioCommandValidator().Validate(cmd);

        Assert.Equal(valido, r.IsValid);
    }
}
