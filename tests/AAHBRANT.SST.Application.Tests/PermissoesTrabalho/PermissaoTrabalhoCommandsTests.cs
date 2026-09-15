using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.PermissaoTrabalhoPreRequisitos.Commands;
using AAHBRANT.SST.Application.PermissaoTrabalhoVerificacoes.Commands;
using AAHBRANT.SST.Application.PermissoesTrabalho.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.PermissoesTrabalho;

public class PermissaoTrabalhoCommandsTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Guid atividadeId, Guid usuarioId)> SemearBaseAsync(IAppDbContext db)
    {
        var atividade = new Atividade { Nome = "Solda em tubulação" };
        db.Atividades.Add(atividade);
        var usuario = new Usuario { Nome = "Responsável Teste", Email = $"{Guid.NewGuid()}@teste.com" };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return (atividade.Id, usuario.Id);
    }

    private static async Task<Guid> CriarPtAsync(IAppDbContext db, Guid atividadeId)
    {
        var handler = new CriarPermissaoTrabalhoCommandHandler(db, new GeradorNumeroDocumentoService(db));
        return await handler.Handle(new CriarPermissaoTrabalhoCommand(
            atividadeId, "Solda em tubulação de gás", "Frente 2",
            "Empresa Executante Ltda", null, DateTime.UtcNow, null, null, null,
            null, null, new List<Guid>()), default);
    }

    [Fact]
    public async Task Criar_SemeiaOs6PreRequisitosEAs15VerificacoesEmBranco()
    {
        var db = CriarDb(nameof(Criar_SemeiaOs6PreRequisitosEAs15VerificacoesEmBranco));
        var (atividadeId, _) = await SemearBaseAsync(db);

        var id = await CriarPtAsync(db, atividadeId);

        var preRequisitos = await db.PermissaoTrabalhoPreRequisitos.Where(r => r.PermissaoTrabalhoId == id).ToListAsync();
        var verificacoes = await db.PermissaoTrabalhoVerificacoes.Where(v => v.PermissaoTrabalhoId == id).ToListAsync();

        Assert.Equal(Enum.GetValues<ItemPreRequisitoPt>().Length, preRequisitos.Count);
        Assert.All(preRequisitos, r => Assert.False(r.Atendido));
        Assert.Equal(Enum.GetValues<ItemVerificacaoPt>().Length, verificacoes.Count);
        Assert.All(verificacoes, v => Assert.Null(v.Resposta));

        var pt = await db.PermissoesTrabalho.SingleAsync(p => p.Id == id);
        Assert.Equal(StatusPt.EmElaboracao, pt.Status);
    }

    [Fact]
    public async Task Autorizar_ComPreRequisitoPendente_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Autorizar_ComPreRequisitoPendente_LancaInvalidOperationException));
        var (atividadeId, usuarioId) = await SemearBaseAsync(db);
        var id = await CriarPtAsync(db, atividadeId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AutorizarPermissaoTrabalhoCommandHandler(db).Handle(new AutorizarPermissaoTrabalhoCommand(id, usuarioId, null), default));

        Assert.Contains("pré-requisito", ex.Message);
    }

    [Fact]
    public async Task Autorizar_ComVerificacaoNaoConforme_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Autorizar_ComVerificacaoNaoConforme_LancaInvalidOperationException));
        var (atividadeId, usuarioId) = await SemearBaseAsync(db);
        var id = await CriarPtAsync(db, atividadeId);

        foreach (var pr in await db.PermissaoTrabalhoPreRequisitos.Where(r => r.PermissaoTrabalhoId == id).ToListAsync())
            await new MarcarPermissaoTrabalhoPreRequisitoCommandHandler(db).Handle(new MarcarPermissaoTrabalhoPreRequisitoCommand(pr.Id, true), default);

        var verificacao = await db.PermissaoTrabalhoVerificacoes.FirstAsync(v => v.PermissaoTrabalhoId == id);
        await new ResponderPermissaoTrabalhoVerificacaoCommandHandler(db).Handle(
            new ResponderPermissaoTrabalhoVerificacaoCommand(verificacao.Id, RespostaVerificacaoPt.NaoConforme), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AutorizarPermissaoTrabalhoCommandHandler(db).Handle(new AutorizarPermissaoTrabalhoCommand(id, usuarioId, null), default));

        Assert.Contains("Não Conforme", ex.Message);
    }

    [Fact]
    public async Task Autorizar_ComTudoAtendidoENenhumaNaoConforme_LiberaAPt()
    {
        var db = CriarDb(nameof(Autorizar_ComTudoAtendidoENenhumaNaoConforme_LiberaAPt));
        var (atividadeId, usuarioId) = await SemearBaseAsync(db);
        var id = await CriarPtAsync(db, atividadeId);

        foreach (var pr in await db.PermissaoTrabalhoPreRequisitos.Where(r => r.PermissaoTrabalhoId == id).ToListAsync())
            await new MarcarPermissaoTrabalhoPreRequisitoCommandHandler(db).Handle(new MarcarPermissaoTrabalhoPreRequisitoCommand(pr.Id, true), default);

        await new AutorizarPermissaoTrabalhoCommandHandler(db).Handle(new AutorizarPermissaoTrabalhoCommand(id, usuarioId, null), default);

        var pt = await db.PermissoesTrabalho.SingleAsync(p => p.Id == id);
        Assert.Equal(StatusPt.Autorizada, pt.Status);
        Assert.Equal(usuarioId, pt.AutorizadoPorUsuarioId);
        Assert.NotNull(pt.DataAutorizacao);
        Assert.NotNull(pt.DataAssinaturaExecucao);
    }

    private static async Task<Guid> CriarELiberarPtAsync(IAppDbContext db, Guid atividadeId, Guid usuarioId)
    {
        var id = await CriarPtAsync(db, atividadeId);
        foreach (var pr in await db.PermissaoTrabalhoPreRequisitos.Where(r => r.PermissaoTrabalhoId == id).ToListAsync())
            await new MarcarPermissaoTrabalhoPreRequisitoCommandHandler(db).Handle(new MarcarPermissaoTrabalhoPreRequisitoCommand(pr.Id, true), default);
        await new AutorizarPermissaoTrabalhoCommandHandler(db).Handle(new AutorizarPermissaoTrabalhoCommand(id, usuarioId, null), default);
        return id;
    }

    [Fact]
    public async Task Suspender_PtAutorizada_MudaParaSuspensaERegistraMotivo()
    {
        var db = CriarDb(nameof(Suspender_PtAutorizada_MudaParaSuspensaERegistraMotivo));
        var (atividadeId, usuarioId) = await SemearBaseAsync(db);
        var id = await CriarELiberarPtAsync(db, atividadeId, usuarioId);

        await new SuspenderPermissaoTrabalhoCommandHandler(db).Handle(
            new SuspenderPermissaoTrabalhoCommand(id, "Condição meteorológica adversa", usuarioId), default);

        var pt = await db.PermissoesTrabalho.SingleAsync(p => p.Id == id);
        Assert.Equal(StatusPt.Suspensa, pt.Status);
        Assert.Equal("Condição meteorológica adversa", pt.MotivoSuspensao);
        Assert.NotNull(pt.DataSuspensao);
    }

    [Fact]
    public async Task Suspender_PtEmElaboracao_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Suspender_PtEmElaboracao_LancaInvalidOperationException));
        var (atividadeId, usuarioId) = await SemearBaseAsync(db);
        var id = await CriarPtAsync(db, atividadeId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SuspenderPermissaoTrabalhoCommandHandler(db).Handle(new SuspenderPermissaoTrabalhoCommand(id, "Motivo qualquer", usuarioId), default));
    }

    [Fact]
    public async Task Revalidar_PtSuspensa_VoltaParaAutorizadaComNovaValidade()
    {
        var db = CriarDb(nameof(Revalidar_PtSuspensa_VoltaParaAutorizadaComNovaValidade));
        var (atividadeId, usuarioId) = await SemearBaseAsync(db);
        var id = await CriarELiberarPtAsync(db, atividadeId, usuarioId);
        await new SuspenderPermissaoTrabalhoCommandHandler(db).Handle(new SuspenderPermissaoTrabalhoCommand(id, "Motivo", usuarioId), default);

        var novaValidade = DateTime.UtcNow.AddDays(1);
        await new RevalidarPermissaoTrabalhoCommandHandler(db).Handle(
            new RevalidarPermissaoTrabalhoCommand(id, novaValidade, null, usuarioId), default);

        var pt = await db.PermissoesTrabalho.SingleAsync(p => p.Id == id);
        Assert.Equal(StatusPt.Autorizada, pt.Status);
        Assert.Equal(novaValidade, pt.Validade);
        Assert.Equal(usuarioId, pt.RevalidadaPorUsuarioId);
        Assert.NotNull(pt.DataRevalidacao);
    }

    [Fact]
    public async Task Encerrar_PtAutorizada_MudaParaEncerradaERegistraObservacoes()
    {
        var db = CriarDb(nameof(Encerrar_PtAutorizada_MudaParaEncerradaERegistraObservacoes));
        var (atividadeId, usuarioId) = await SemearBaseAsync(db);
        var id = await CriarELiberarPtAsync(db, atividadeId, usuarioId);

        await new EncerrarPermissaoTrabalhoCommandHandler(db).Handle(
            new EncerrarPermissaoTrabalhoCommand(id, usuarioId, "Área inspecionada, limpa, segura e liberada."), default);

        var pt = await db.PermissoesTrabalho.SingleAsync(p => p.Id == id);
        Assert.Equal(StatusPt.Encerrada, pt.Status);
        Assert.Equal("Área inspecionada, limpa, segura e liberada.", pt.ObservacoesEncerramento);
        Assert.NotNull(pt.DataEncerramento);
    }

    [Fact]
    public async Task Encerrar_PtSuspensa_PermiteEncerrarDiretamente()
    {
        var db = CriarDb(nameof(Encerrar_PtSuspensa_PermiteEncerrarDiretamente));
        var (atividadeId, usuarioId) = await SemearBaseAsync(db);
        var id = await CriarELiberarPtAsync(db, atividadeId, usuarioId);
        await new SuspenderPermissaoTrabalhoCommandHandler(db).Handle(new SuspenderPermissaoTrabalhoCommand(id, "Motivo", usuarioId), default);

        await new EncerrarPermissaoTrabalhoCommandHandler(db).Handle(
            new EncerrarPermissaoTrabalhoCommand(id, usuarioId, null), default);

        var pt = await db.PermissoesTrabalho.SingleAsync(p => p.Id == id);
        Assert.Equal(StatusPt.Encerrada, pt.Status);
    }
}
