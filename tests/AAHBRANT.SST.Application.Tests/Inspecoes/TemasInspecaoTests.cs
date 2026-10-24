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
        // Foto só é exigida nos itens Não Conforme (regra do encerramento), nunca em todos.
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

        foreach (var r in db.InspecaoItemRespostas.Where(r => r.InspecaoId == atual.InspecaoId)) r.StatusItem = StatusItemChecklist.Conforme;
        await db.SaveChangesAsync();
        await new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(atual.InspecaoId), default);

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

    // Mesma regra dos Veículos: foto obrigatória só no item Não Conforme.
    [Theory]
    [InlineData(StatusItemChecklist.NaoConforme, false)]
    [InlineData(StatusItemChecklist.Conforme, true)]
    [InlineData(StatusItemChecklist.NaoAplicavel, true)]
    public async Task Encerrar_FotoSoEhExigidaNoItemNaoConforme(StatusItemChecklist status, bool conclui)
    {
        var (db, obra, temaId) = await CenarioAsync("Guarda-corpo instalado");
        var itemId = await new CriarItemInspecionavelCommandHandler(db).Handle(new CriarItemInspecionavelCommand(temaId, obra.Id, "Andaime A-03", null), default);
        var atual = await new ObterOuCriarInspecaoItemCommandHandler(db, new GeradorNumeroDocumentoService(db))
            .Handle(new ObterOuCriarInspecaoItemCommand(itemId, AzureAdObjectId), default);
        (await db.InspecaoItemRespostas.SingleAsync(r => r.InspecaoId == atual.InspecaoId)).StatusItem = status;
        await db.SaveChangesAsync();

        var encerrar = () => new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(atual.InspecaoId), default);

        if (conclui)
        {
            await encerrar();
            Assert.Equal(StatusInspecao.Concluida, (await db.Inspecoes.SingleAsync()).Status);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(encerrar);
            Assert.Contains("exigem foto", ex.Message);
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
}
