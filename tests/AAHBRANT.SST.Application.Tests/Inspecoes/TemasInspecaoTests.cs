using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Application.Inspecoes.Temas;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Inspecoes;

// Temas de inspeção criados pelo usuário (24/10/2026): card por tema com itens cadastrados por obra,
// cada item com o seu histórico de inspeções — mesmo desenho dos Veículos.
public class TemasInspecaoTests
{
    private const string AzureAdObjectId = "oid-tecnico-tema";

    private static async Task<(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext Db, Obra Obra, Guid TemaId)> CenarioAsync(params string[] itensChecklist)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.AddRange(obra, new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico", AzureAdObjectId = AzureAdObjectId });
        await db.SaveChangesAsync();
        var itens = itensChecklist.Length > 0 ? itensChecklist : new[] { "Guarda-corpo instalado", "Piso completo" };
        var temaId = await new CriarTemaInspecaoCommandHandler(db).Handle(
            new CriarTemaInspecaoCommand("Andaime", itens.Select(i => new CriarTemaInspecaoItemInput(i)).ToList()), default);
        return (db, obra, temaId);
    }

    [Fact]
    public async Task CriarTema_GeraOChecklistV1ComOsItensEmOrdem()
    {
        var (db, _, temaId) = await CenarioAsync("Guarda-corpo instalado", "Piso completo", "Rodapé");

        var checklist = await db.ChecklistModelos.Include(c => c.Itens).SingleAsync(c => c.TemaInspecaoId == temaId);
        Assert.Equal(TipoInspecao.Tema, checklist.TipoInspecao);
        Assert.Equal(1, checklist.Versao);
        Assert.Equal(new[] { "Guarda-corpo instalado", "Piso completo", "Rodapé" }, checklist.Itens.OrderBy(i => i.Ordem).Select(i => i.Descricao));
        // A foto de cada item vem da regra do encerramento (todos os itens do tema), não do modelo.
        Assert.All(checklist.Itens, i => Assert.False(i.ExigeFotografia));
    }

    [Theory]
    [InlineData("Alojamento")]
    [InlineData("  veículos ")]
    public async Task CriarTema_RecusaNomeDeCardFixo(string nome)
    {
        var (db, _, _) = await CenarioAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new CriarTemaInspecaoCommandHandler(db).Handle(
            new CriarTemaInspecaoCommand(nome, new() { new CriarTemaInspecaoItemInput("Item") }), default));

        Assert.Contains("card fixo", ex.Message);
    }

    [Fact]
    public async Task CriarTema_RecusaNomeRepetidoSemDiferenciarMaiusculas()
    {
        var (db, _, _) = await CenarioAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new CriarTemaInspecaoCommandHandler(db).Handle(
            new CriarTemaInspecaoCommand("ANDAIME", new() { new CriarTemaInspecaoItemInput("Item") }), default));

        Assert.Contains("Já existe um tema", ex.Message);
    }

    [Fact]
    public void CriarTema_ExigeNomeEAoMenosUmItemComDescricao()
    {
        var validador = new CriarTemaInspecaoCommandValidator();

        Assert.False(validador.Validate(new CriarTemaInspecaoCommand("", new() { new CriarTemaInspecaoItemInput("x") })).IsValid);
        Assert.False(validador.Validate(new CriarTemaInspecaoCommand("Escada", new())).IsValid);
        Assert.False(validador.Validate(new CriarTemaInspecaoCommand("Escada", new() { new CriarTemaInspecaoItemInput(" ") })).IsValid);
        Assert.True(validador.Validate(new CriarTemaInspecaoCommand("Escada", new() { new CriarTemaInspecaoItemInput("Degraus firmes") })).IsValid);
    }

    [Fact]
    public async Task CriarItem_RecusaIdentificacaoRepetidaNaMesmaObraETema()
    {
        var (db, obra, temaId) = await CenarioAsync();
        var criar = new CriarItemInspecionavelCommandHandler(db);
        await criar.Handle(new CriarItemInspecionavelCommand(temaId, obra.Id, "Andaime A-03", null), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => criar.Handle(new CriarItemInspecionavelCommand(temaId, obra.Id, "andaime a-03", null), default));

        Assert.Contains("Já existe", ex.Message);
    }

    [Fact]
    public async Task ListarItens_SemInspecaoMostraNuncaEComInspecaoConcluidaMostraInspecionado()
    {
        var (db, obra, temaId) = await CenarioAsync();
        var itemId = await new CriarItemInspecionavelCommandHandler(db).Handle(new CriarItemInspecionavelCommand(temaId, obra.Id, "Andaime A-03", null), default);
        var listar = new ListarItensInspecionaveisQueryHandler(db);

        var antes = (await listar.Handle(new ListarItensInspecionaveisQuery(temaId, null), default)).Single();
        Assert.Equal("nunca", antes.StatusUltimaInspecao);
        Assert.Empty(antes.HistoricoInspecoes);

        var atual = await new ObterOuCriarInspecaoItemCommandHandler(db, new GeradorNumeroDocumentoService(db))
            .Handle(new ObterOuCriarInspecaoItemCommand(itemId, AzureAdObjectId), default);
        var emAndamento = (await listar.Handle(new ListarItensInspecionaveisQuery(temaId, null), default)).Single();
        Assert.NotNull(emAndamento.InspecaoEmAndamento);

        foreach (var r in db.InspecaoItemRespostas.Where(r => r.InspecaoId == atual.InspecaoId)) { r.StatusItem = StatusItemChecklist.Conforme; r.FotoConteudo = new byte[] { 1 }; }
        await db.SaveChangesAsync();
        await FotosInspecaoDeTeste.CompletarAsync(db, atual.InspecaoId);
        await new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(atual.InspecaoId), default);

        // Finalizar só leva para "Aguardando assinatura": o item continua "nunca inspecionado" até assinar.
        var aguardando = (await listar.Handle(new ListarItensInspecionaveisQuery(temaId, null), default)).Single();
        Assert.Equal("nunca", aguardando.StatusUltimaInspecao);
        (await db.Inspecoes.SingleAsync(i => i.Id == atual.InspecaoId)).Status = StatusInspecao.Concluida; // assinatura finalizada
        await db.SaveChangesAsync();

        var depois = (await listar.Handle(new ListarItensInspecionaveisQuery(temaId, null), default)).Single();
        Assert.Equal("inspecionado", depois.StatusUltimaInspecao);
        Assert.Single(depois.HistoricoInspecoes);
    }

    [Fact]
    public async Task ObterOuCriar_UsaOChecklistDoTemaEPreencheOItem_ERetomaAEmAndamento()
    {
        var (db, obra, temaId) = await CenarioAsync();
        var itemId = await new CriarItemInspecionavelCommandHandler(db).Handle(new CriarItemInspecionavelCommand(temaId, obra.Id, "Andaime A-03", null), default);
        var handler = new ObterOuCriarInspecaoItemCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var primeira = await handler.Handle(new ObterOuCriarInspecaoItemCommand(itemId, AzureAdObjectId), default);
        var segunda = await handler.Handle(new ObterOuCriarInspecaoItemCommand(itemId, AzureAdObjectId), default);

        Assert.True(primeira.FoiCriadaAgora);
        Assert.False(segunda.FoiCriadaAgora);
        Assert.Equal(primeira.InspecaoId, segunda.InspecaoId);
        var inspecao = await db.Inspecoes.SingleAsync();
        Assert.Equal(TipoInspecao.Tema, inspecao.TipoInspecao);
        Assert.Equal(itemId, inspecao.ItemInspecionavelId);
        Assert.Equal(obra.Id, inspecao.ObraId);
        Assert.Equal(2, inspecao.Respostas.Count);
    }

    [Fact]
    public async Task ObterOuCriar_SemUsuarioVinculado_Recusa()
    {
        var (db, obra, temaId) = await CenarioAsync();
        var itemId = await new CriarItemInspecionavelCommandHandler(db).Handle(new CriarItemInspecionavelCommand(temaId, obra.Id, "Andaime A-03", null), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ObterOuCriarInspecaoItemCommandHandler(db, new GeradorNumeroDocumentoService(db))
            .Handle(new ObterOuCriarInspecaoItemCommand(itemId, "oid-desconhecido"), default));
    }

    // 18/11/2026: 2 fotos obrigatórias em TODOS os itens, qualquer que seja a resposta (antes: 1 foto, 24/10).
    [Theory]
    [InlineData(StatusItemChecklist.NaoConforme, 0, false)]
    [InlineData(StatusItemChecklist.Conforme, 0, false)]
    [InlineData(StatusItemChecklist.NaoAplicavel, 0, false)]
    [InlineData(StatusItemChecklist.Conforme, 1, false)]
    [InlineData(StatusItemChecklist.NaoAplicavel, 2, true)]
    [InlineData(StatusItemChecklist.Conforme, 2, true)]
    [InlineData(StatusItemChecklist.NaoConforme, 2, true)]
    public async Task Encerrar_DuasFotosSaoExigidasEmTodosOsItens(StatusItemChecklist status, int fotos, bool conclui)
    {
        var (db, obra, temaId) = await CenarioAsync("Guarda-corpo instalado");
        var itemId = await new CriarItemInspecionavelCommandHandler(db).Handle(new CriarItemInspecionavelCommand(temaId, obra.Id, "Andaime A-03", null), default);
        var atual = await new ObterOuCriarInspecaoItemCommandHandler(db, new GeradorNumeroDocumentoService(db))
            .Handle(new ObterOuCriarInspecaoItemCommand(itemId, AzureAdObjectId), default);
        var resposta = await db.InspecaoItemRespostas.SingleAsync(r => r.InspecaoId == atual.InspecaoId);
        resposta.StatusItem = status;
        if (fotos >= 1) resposta.FotoConteudo = new byte[] { 1 };
        await db.SaveChangesAsync();
        if (fotos >= 2) await FotosInspecaoDeTeste.CompletarAsync(db, atual.InspecaoId);

        var encerrar = () => new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(atual.InspecaoId), default);

        if (conclui)
        {
            await encerrar();
            Assert.Equal(StatusInspecao.AguardandoAssinatura, (await db.Inspecoes.SingleAsync()).Status);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(encerrar);
            Assert.Contains("exige 2 fotos", ex.Message);
            Assert.Contains("Guarda-corpo instalado", ex.Message);
        }
    }

    [Fact]
    public async Task NovaInspecaoGenerica_RecusaChecklistDeTema()
    {
        var (db, obra, temaId) = await CenarioAsync();
        var checklistId = (await db.ChecklistModelos.SingleAsync(c => c.TemaInspecaoId == temaId)).Id;
        var usuario = await db.Usuarios.SingleAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new CriarInspecaoCommandHandler(db, new GeradorNumeroDocumentoService(db))
            .Handle(new CriarInspecaoCommand(checklistId, obra.Id, null, DateTime.UtcNow, usuario.Id), default));

        Assert.Contains("card do tema", ex.Message);
    }

    // A nova versão do checklist precisa continuar achável pelo tema, senão o card perderia o checklist ao editar.
    [Fact]
    public async Task NovaVersaoDoChecklist_MantemOVinculoComOTema()
    {
        var (db, _, temaId) = await CenarioAsync();
        var v1 = await db.ChecklistModelos.SingleAsync(c => c.TemaInspecaoId == temaId);

        var v2Id = await new AAHBRANT.SST.Application.ChecklistModelos.Commands.CriarNovaVersaoChecklistModeloCommandHandler(db).Handle(
            new AAHBRANT.SST.Application.ChecklistModelos.Commands.CriarNovaVersaoChecklistModeloCommand(
                v1.Id, new() { new("Guarda-corpo instalado", false, false, false), new("Rodapé", false, false, false) }), default);

        var v2 = await db.ChecklistModelos.SingleAsync(c => c.Id == v2Id);
        Assert.Equal(temaId, v2.TemaInspecaoId);
        Assert.Equal(TipoInspecao.Tema, v2.TipoInspecao);
        var resumo = (await new ListarTemasInspecaoQueryHandler(db).Handle(new ListarTemasInspecaoQuery(), default)).Single();
        Assert.Equal(v2Id, resumo.ChecklistModeloId);
        Assert.Equal(2, resumo.TotalItensChecklist);
    }

    // Ponto de prazo no card do tema (25/11/2026): quantos itens vencem em até 7 dias e quantos já
    // passaram do prazo. Sem periodicidade, o item não conta em nenhum dos dois.
    [Fact]
    public async Task ListarTemas_ContaItensVencendoEForaDoPrazo()
    {
        var (db, obra, temaId) = await CenarioAsync();
        ItemInspecionavel Item(string id, int? dias) => new() { TemaInspecaoId = temaId, ObraId = obra.Id, Identificacao = id, PeriodicidadeInspecaoDias = dias };
        var foraDoPrazo = Item("A-01", 30);
        var vencendo = Item("A-02", 30);
        var emDia = Item("A-03", 30);
        var semPeriodicidade = Item("A-04", null);
        db.AddRange(foraDoPrazo, vencendo, emDia, semPeriodicidade);
        await db.SaveChangesAsync();
        // Nunca inspecionados: o prazo conta do cadastro.
        var hoje = DateTime.UtcNow.Date;
        foraDoPrazo.CreatedAtUtc = hoje.AddDays(-40);
        vencendo.CreatedAtUtc = hoje.AddDays(-25);
        emDia.CreatedAtUtc = hoje.AddDays(-5);
        semPeriodicidade.CreatedAtUtc = hoje.AddDays(-400);
        await db.SaveChangesAsync();

        var resumo = (await new ListarTemasInspecaoQueryHandler(db).Handle(new ListarTemasInspecaoQuery(), default)).Single();

        Assert.Equal(4, resumo.TotalItens);
        Assert.Equal(1, resumo.ItensForaDoPrazo);
        Assert.Equal(1, resumo.ItensVencendo);
    }
}
