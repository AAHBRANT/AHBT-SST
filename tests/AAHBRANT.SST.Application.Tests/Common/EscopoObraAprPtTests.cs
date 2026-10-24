using AAHBRANT.SST.Application.AprEtapas.Commands;
using AAHBRANT.SST.Application.AprEtapas.Queries;
using AAHBRANT.SST.Application.Aprs.Commands;
using AAHBRANT.SST.Application.Aprs.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.PermissaoTrabalhoPreRequisitos.Commands;
using AAHBRANT.SST.Application.PermissoesTrabalho.Commands;
using AAHBRANT.SST.Application.PermissoesTrabalho.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Common;

// Auditoria de 09/10/2026: APR e PT chegam à obra pela Atividade. A leitura já ficava protegida pelo
// INNER JOIN com a Atividade filtrada, mas a escrita por id não — um técnico da obra A aprovava APR e
// autorizava/encerrava PT da obra B. Usuário restrito à obra A não pode alterar nada da obra B.
public class EscopoObraAprPtTests
{
    private static readonly Guid ObraA = Guid.NewGuid();
    private static readonly Guid ObraB = Guid.NewGuid();

    private sealed class GeradorNumeroFake : IGeradorNumeroDocumentoService
    {
        public Task<string> GerarAsync(string prefixo, CancellationToken ct) => Task.FromResult($"{prefixo}-TESTE-001");
    }

    private sealed record Cenario(
        SstDbContext Global, SstDbContext Restrito,
        Usuario Usuario,
        Atividade AtividadeA, Atividade AtividadeB,
        Apr AprA, Apr AprB,
        AprEtapa EtapaB,
        PermissaoTrabalho PtA, PermissaoTrabalho PtB,
        PermissaoTrabalho PtBAutorizada);

    private static async Task<Cenario> CriarAsync()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var global = new SstDbContext(opcoes, new CurrentUserService());
        var usuarioRestrito = new CurrentUserService();
        usuarioRestrito.DefinirEscopo(false, new[] { ObraA });
        var restrito = new SstDbContext(opcoes, usuarioRestrito);

        var usuario = new Usuario { Nome = "Técnico", Email = "tec@x.com" };
        var atividadeA = new Atividade { ObraId = ObraA, Nome = "Atividade A" };
        var atividadeB = new Atividade { ObraId = ObraB, Nome = "Atividade B" };
        global.Obras.AddRange(new Obra { Id = ObraA, Codigo = "OB-A", Nome = "Obra A" }, new Obra { Id = ObraB, Codigo = "OB-B", Nome = "Obra B" });
        global.Usuarios.Add(usuario);
        global.Atividades.AddRange(atividadeA, atividadeB);
        await global.SaveChangesAsync();

        var hoje = DateTime.UtcNow;
        var aprA = new Apr { NumeroApr = "APR-A", AtividadeId = atividadeA.Id, Local = "Local A", Data = hoje };
        var aprB = new Apr { NumeroApr = "APR-B", AtividadeId = atividadeB.Id, Local = "Local B", Data = hoje };
        var etapaB = new AprEtapa { Apr = aprB, Ordem = 1, Descricao = "Etapa B" };
        var ptA = new PermissaoTrabalho { NumeroPt = "PT-A", AtividadeId = atividadeA.Id, DescricaoAtividade = "PT A", Local = "Local A", Data = hoje };
        var ptB = new PermissaoTrabalho { NumeroPt = "PT-B", AtividadeId = atividadeB.Id, DescricaoAtividade = "PT B", Local = "Local B", Data = hoje };
        var ptBAutorizada = new PermissaoTrabalho
        {
            NumeroPt = "PT-B2", AtividadeId = atividadeB.Id, DescricaoAtividade = "PT B autorizada", Local = "Local B", Data = hoje,
            Status = StatusPt.Autorizada,
        };
        var preRequisitoB = new PermissaoTrabalhoPreRequisito { PermissaoTrabalho = ptB, Item = Enum.GetValues<ItemPreRequisitoPt>()[0], Atendido = true };
        global.Aprs.AddRange(aprA, aprB);
        global.AprEtapas.Add(etapaB);
        global.PermissoesTrabalho.AddRange(ptA, ptB, ptBAutorizada);
        global.PermissaoTrabalhoPreRequisitos.Add(preRequisitoB);
        await global.SaveChangesAsync();

        return new Cenario(global, restrito, usuario, atividadeA, atividadeB, aprA, aprB, etapaB, ptA, ptB, ptBAutorizada);
    }

    [Fact]
    public async Task Apr_AtualizarDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new AtualizarAprCommandHandler(c.Restrito).Handle(
            new AtualizarAprCommand(c.AprB.Id, c.AtividadeB.Id, "Alterado", null, null, null, DateTime.UtcNow, null, new List<Guid>()), default));

        var aprB = await c.Global.Aprs.AsNoTracking().FirstAsync(a => a.Id == c.AprB.Id);
        Assert.Equal("Local B", aprB.Local);
    }

    [Fact]
    public async Task Apr_MoverAprPropriaParaAtividadeDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new AtualizarAprCommandHandler(c.Restrito).Handle(
            new AtualizarAprCommand(c.AprA.Id, c.AtividadeB.Id, "Local A", null, null, null, DateTime.UtcNow, null, new List<Guid>()), default));
    }

    [Fact]
    public async Task Apr_AprovarReprovarExcluirDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new AprovarAprCommandHandler(c.Restrito).Handle(new AprovarAprCommand(c.AprB.Id, c.Usuario.Id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ReprovarAprCommandHandler(c.Restrito).Handle(new ReprovarAprCommand(c.AprB.Id, "motivo"), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ExcluirAprCommandHandler(c.Restrito).Handle(new ExcluirAprCommand(c.AprB.Id), default));

        var aprB = await c.Global.Aprs.AsNoTracking().FirstAsync(a => a.Id == c.AprB.Id);
        Assert.Equal(StatusApr.EmElaboracao, aprB.Status);
    }

    [Fact]
    public async Task Apr_AprovarDaPropriaObra_Funciona()
    {
        var c = await CriarAsync();

        await new AprovarAprCommandHandler(c.Restrito).Handle(new AprovarAprCommand(c.AprA.Id, c.Usuario.Id), default);

        var aprA = await c.Global.Aprs.AsNoTracking().FirstAsync(a => a.Id == c.AprA.Id);
        Assert.Equal(StatusApr.Aprovada, aprA.Status);
    }

    [Fact]
    public async Task Apr_CriarEmAtividadeDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new CriarAprCommandHandler(c.Restrito, new GeradorNumeroFake()).Handle(
            new CriarAprCommand(c.AtividadeB.Id, "Local", null, null, null, DateTime.UtcNow, null, new List<Guid>()), default));

        var id = await new CriarAprCommandHandler(c.Restrito, new GeradorNumeroFake()).Handle(
            new CriarAprCommand(c.AtividadeA.Id, "Local", null, null, null, DateTime.UtcNow, null, new List<Guid>()), default);
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task AprEtapa_ExcluirCriarListarDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ExcluirAprEtapaCommandHandler(c.Restrito).Handle(new ExcluirAprEtapaCommand(c.EtapaB.Id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new CriarAprEtapaCommandHandler(c.Restrito).Handle(new CriarAprEtapaCommand(c.AprB.Id, 2, "Nova"), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ListarAprEtapasQueryHandler(c.Restrito).Handle(new ListarAprEtapasQuery(c.AprB.Id), default));

        Assert.True(await c.Global.AprEtapas.AnyAsync(e => e.Id == c.EtapaB.Id));
    }

    [Fact]
    public async Task Apr_ListarEDetalhe_RestritoVeSoAPropriaObra_GlobalVeTodas()
    {
        var c = await CriarAsync();

        var restrito = await new ListarAprsQueryHandler(c.Restrito).Handle(new ListarAprsQuery(null), default);
        var global = await new ListarAprsQueryHandler(c.Global).Handle(new ListarAprsQuery(null), default);

        Assert.Equal(new[] { c.AprA.Id }, restrito.Select(a => a.Id));
        Assert.Equal(2, global.Count);
        Assert.Null(await new ObterAprDetalheQueryHandler(c.Restrito).Handle(new ObterAprDetalheQuery(c.AprB.Id), default));
        Assert.NotNull(await new ObterAprDetalheQueryHandler(c.Global).Handle(new ObterAprDetalheQuery(c.AprB.Id), default));
    }

    [Fact]
    public async Task Pt_AutorizarDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new AutorizarPermissaoTrabalhoCommandHandler(c.Restrito).Handle(
            new AutorizarPermissaoTrabalhoCommand(c.PtB.Id, c.Usuario.Id, null), default));

        var ptB = await c.Global.PermissoesTrabalho.AsNoTracking().FirstAsync(p => p.Id == c.PtB.Id);
        Assert.Equal(StatusPt.EmElaboracao, ptB.Status);
    }

    [Fact]
    public async Task Pt_EncerrarSuspenderExcluirDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new EncerrarPermissaoTrabalhoCommandHandler(c.Restrito).Handle(
            new EncerrarPermissaoTrabalhoCommand(c.PtBAutorizada.Id, c.Usuario.Id, null), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new SuspenderPermissaoTrabalhoCommandHandler(c.Restrito).Handle(
            new SuspenderPermissaoTrabalhoCommand(c.PtBAutorizada.Id, "motivo", c.Usuario.Id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ExcluirPermissaoTrabalhoCommandHandler(c.Restrito).Handle(new ExcluirPermissaoTrabalhoCommand(c.PtB.Id), default));

        var ptB = await c.Global.PermissoesTrabalho.AsNoTracking().FirstAsync(p => p.Id == c.PtBAutorizada.Id);
        Assert.Equal(StatusPt.Autorizada, ptB.Status);
    }

    [Fact]
    public async Task Pt_MarcarPreRequisitoDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();
        var preRequisitoB = await c.Global.PermissaoTrabalhoPreRequisitos.AsNoTracking().FirstAsync(r => r.PermissaoTrabalhoId == c.PtB.Id);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new MarcarPermissaoTrabalhoPreRequisitoCommandHandler(c.Restrito).Handle(
            new MarcarPermissaoTrabalhoPreRequisitoCommand(preRequisitoB.Id, true), default));
    }

    [Fact]
    public async Task Pt_CriarEmAtividadeDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new CriarPermissaoTrabalhoCommandHandler(c.Restrito, new GeradorNumeroFake()).Handle(
            new CriarPermissaoTrabalhoCommand(c.AtividadeB.Id, "Desc", "Local", null, null, DateTime.UtcNow, null, null, null, null, null, new List<Guid>()), default));
    }

    [Fact]
    public async Task Pt_AutorizarEEncerrarDaPropriaObra_Funciona()
    {
        var c = await CriarAsync();

        await new AutorizarPermissaoTrabalhoCommandHandler(c.Restrito).Handle(
            new AutorizarPermissaoTrabalhoCommand(c.PtA.Id, c.Usuario.Id, null), default);
        await new EncerrarPermissaoTrabalhoCommandHandler(c.Restrito).Handle(
            new EncerrarPermissaoTrabalhoCommand(c.PtA.Id, c.Usuario.Id, null), default);

        var ptA = await c.Global.PermissoesTrabalho.AsNoTracking().FirstAsync(p => p.Id == c.PtA.Id);
        Assert.Equal(StatusPt.Encerrada, ptA.Status);
    }

    [Fact]
    public async Task Global_AutorizaPtEAprovaAprDeQualquerObra()
    {
        var c = await CriarAsync();

        await new AutorizarPermissaoTrabalhoCommandHandler(c.Global).Handle(
            new AutorizarPermissaoTrabalhoCommand(c.PtB.Id, c.Usuario.Id, null), default);
        await new AprovarAprCommandHandler(c.Global).Handle(new AprovarAprCommand(c.AprB.Id, c.Usuario.Id), default);

        Assert.Equal(StatusPt.Autorizada, (await c.Global.PermissoesTrabalho.AsNoTracking().FirstAsync(p => p.Id == c.PtB.Id)).Status);
        Assert.Equal(StatusApr.Aprovada, (await c.Global.Aprs.AsNoTracking().FirstAsync(a => a.Id == c.AprB.Id)).Status);
    }

    [Fact]
    public async Task Pt_ListarEDetalhe_RestritoVeSoAPropriaObra()
    {
        var c = await CriarAsync();

        var restrito = await new ListarPermissoesTrabalhoQueryHandler(c.Restrito).Handle(new ListarPermissoesTrabalhoQuery(null), default);

        Assert.Equal(new[] { c.PtA.Id }, restrito.Select(p => p.Id));
        Assert.Null(await new ObterPermissaoTrabalhoDetalheQueryHandler(c.Restrito).Handle(new ObterPermissaoTrabalhoDetalheQuery(c.PtB.Id), default));
        Assert.NotNull(await new ObterPermissaoTrabalhoDetalheQueryHandler(c.Global).Handle(new ObterPermissaoTrabalhoDetalheQuery(c.PtB.Id), default));
    }
}
