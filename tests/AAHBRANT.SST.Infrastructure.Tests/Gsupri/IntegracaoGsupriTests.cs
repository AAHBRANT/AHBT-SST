using AAHBRANT.SST.Application.IntegracaoGsupri;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Integracao.Gsupri;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AAHBRANT.SST.Infrastructure.Tests.Gsupri;

public class IntegracaoGsupriTests
{
    private static async Task<(SstDbContext Db, IntegracaoGsupriService Service, Obra Obra, CatalogoEpi Epi)> Criar(bool vincular = true)
    {
        var db = new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new CurrentUserService());
        var service = new IntegracaoGsupriService(db, new CurrentUserService(), new ConfigurationBuilder().Build());
        var obra = new Obra { Nome = "Obra de teste", Codigo = "TESTE" };
        var epi = new CatalogoEpi { Nome = "Luva" };
        db.AddRange(obra, epi); await db.SaveChangesAsync();
        if (vincular)
        {
            await service.VincularObraAsync(new("OBRA-1", obra.Id), default);
            await service.VincularProdutoAsync(new("LUVA", "CX", "EPI", epi.Id, null, 100), default);
        }
        return (db, service, obra, epi);
    }
    private static RecebimentoGsupriPayload Evento(string evento = "EV-1", string recebimento = "REC-1", int versao = 1, decimal qtd = 2) =>
        new(evento, recebimento, versao, "PED-1", "OBRA-1", "12345678000190", "4544", null,
            new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero), false,
            [new("1", "LUVA", "Luva caixa 100", "CX", qtd, null, false)]);

    [Fact] public async Task ReenvioMesmoEventoOuNovoEnvelope_NaoDuplicaSaldo()
    {
        var (db, s, _, _) = await Criar();
        await s.ReceberAsync(Evento(), default);
        await s.ReceberAsync(Evento(), default);
        var r = await s.ReceberAsync(Evento("EV-2"), default);
        await s.ReprocessarAsync(r.Id, default);
        Assert.Equal(200, (await db.EstoquesEpi.SingleAsync()).Saldo);
        Assert.Single(await db.MovimentacoesEstoqueEpi.ToListAsync());
        Assert.Equal(2, await db.Set<GsupriEvento>().CountAsync());
    }

    [Fact] public async Task DoisItensMesmoProduto_SomamUmaMovimentacao()
    {
        var (db, s, _, _) = await Criar(); var e = Evento();
        e.Itens.Add(e.Itens[0] with { ItemId = "2", QuantidadeRecebida = 3 });
        await s.ReceberAsync(e, default);
        Assert.Equal(500, (await db.EstoquesEpi.SingleAsync()).Saldo);
        Assert.Single(await db.EstoquesEpi.ToListAsync());
    }

    [Fact] public async Task RecebimentosParciaisDistintosMesmaNota_Somam()
    {
        var (db, s, _, _) = await Criar();
        await s.ReceberAsync(Evento(), default);
        await s.ReceberAsync(Evento("EV-2", "REC-2", qtd: 1), default);
        Assert.Equal(300, (await db.EstoquesEpi.SingleAsync()).Saldo);
    }

    [Fact] public async Task Revisao_AplicaSomenteDiferencaEGuardaEventos()
    {
        var (db, s, _, _) = await Criar();
        await s.ReceberAsync(Evento(), default);
        await s.ReceberAsync(Evento("EV-2", versao: 2, qtd: 3), default);
        Assert.Equal(300, (await db.EstoquesEpi.SingleAsync()).Saldo);
        Assert.Equal(new[] { 100, 200 }, await db.MovimentacoesEstoqueEpi.OrderBy(m => m.Quantidade).Select(m => m.Quantidade).ToArrayAsync());
        Assert.All(await db.MovimentacoesEstoqueEpi.ToListAsync(), m => Assert.Equal(TipoMovimentacaoEstoqueEpi.IntegracaoGsupri, m.Tipo));
    }

    [Fact] public async Task NaoConformidadeSemLiberacao_FicaPendenteSemSaldo()
    {
        var (db, s, _, _) = await Criar(); var e = Evento();
        e.Itens[0] = e.Itens[0] with { TemNaoConformidade = true };
        var r = await s.ReceberAsync(e, default);
        Assert.Equal("Pendente", r.Status); Assert.Empty(await db.EstoquesEpi.ToListAsync());
        e = e with { EventoId = "EV-2", Versao = 2 };
        e.Itens[0] = e.Itens[0] with { QuantidadeLiberada = 1 };
        r = await s.ReceberAsync(e, default);
        Assert.Equal("Pendente", r.Status); Assert.Equal(100, (await db.EstoquesEpi.SingleAsync()).Saldo);
    }

    [Fact] public async Task SemVinculos_PreservaRecebimentoEReprocessaDepois()
    {
        var (db, s, obra, epi) = await Criar(false);
        var r = await s.ReceberAsync(Evento(), default);
        Assert.Equal("Pendente", r.Status); Assert.Null(r.ObraId);
        await s.VincularObraAsync(new("OBRA-1", obra.Id), default);
        r = await s.ReprocessarAsync(r.Id, default);
        Assert.NotNull(r.Itens[0].Pendencia);
        await s.VincularProdutoAsync(new("LUVA", "CX", "EPI", epi.Id, null, 100), default);
        r = await s.ReprocessarAsync(r.Id, default);
        Assert.Equal("Processado", r.Status); Assert.Equal(200, (await db.EstoquesEpi.SingleAsync()).Saldo);
    }

    [Fact] public async Task Cancelamento_EstornaSemApagarHistorico()
    {
        var (db, s, _, _) = await Criar();
        await s.ReceberAsync(Evento(), default);
        var cancelamento = Evento("EV-2", versao: 2) with { Cancelado = true, Itens = [] };
        var r = await s.ReceberAsync(cancelamento, default);
        Assert.Equal("Cancelado", r.Status); Assert.Equal(0, (await db.EstoquesEpi.SingleAsync()).Saldo);
        Assert.Equal(2, await db.MovimentacoesEstoqueEpi.CountAsync());
        await s.ReprocessarAsync(r.Id, default);
        Assert.Equal(2, await db.MovimentacoesEstoqueEpi.CountAsync());
    }

    [Fact] public async Task CancelamentoComMaterialJaEntregue_NaoNegativaSaldo()
    {
        var (db, s, _, _) = await Criar();
        await s.ReceberAsync(Evento(), default);
        var estoque = await db.EstoquesEpi.SingleAsync(); estoque.Saldo = 50; await db.SaveChangesAsync();
        var r = await s.ReceberAsync(Evento("EV-2", versao: 2) with { Cancelado = true, Itens = [] }, default);
        Assert.Equal("PendenteRegularizacao", r.Status); Assert.Equal(50, estoque.Saldo);
        Assert.Single(await db.MovimentacoesEstoqueEpi.ToListAsync());
        estoque.Saldo = 200; await db.SaveChangesAsync();
        r = await s.ReprocessarAsync(r.Id, default);
        Assert.Equal("Cancelado", r.Status); Assert.Equal(0, estoque.Saldo);
    }

    [Fact] public async Task MesmaVersaoOuEventoComDadosDiferentes_Recusa()
    {
        var (db, s, _, _) = await Criar(); await s.ReceberAsync(Evento(), default);
        await Assert.ThrowsAsync<ConflitoGsupriException>(() => s.ReceberAsync(Evento(qtd: 3), default));
        await Assert.ThrowsAsync<ConflitoGsupriException>(() => s.ReceberAsync(Evento("EV-2", qtd: 3), default));
        Assert.Equal(200, (await db.EstoquesEpi.SingleAsync()).Saldo);
    }

    [Fact] public async Task VersaoAntigaOuTrocaDeObra_Recusa()
    {
        var (_, s, _, _) = await Criar(); await s.ReceberAsync(Evento(versao: 2), default);
        await Assert.ThrowsAsync<ConflitoGsupriException>(() => s.ReceberAsync(Evento("EV-2"), default));
        await Assert.ThrowsAsync<ConflitoGsupriException>(() => s.ReceberAsync(Evento("EV-3", versao: 3) with { ObraCodigo = "OUTRA" }, default));
    }

    [Fact] public async Task ForaDoEscopo_NaoCriaEstoque()
    {
        var (db, s, obra, _) = await Criar(false);
        await s.VincularObraAsync(new("OBRA-1", obra.Id), default);
        await s.VincularProdutoAsync(new("LUVA", "CX", "IGNORAR", null, null, 1), default);
        var r = await s.ReceberAsync(Evento(), default);
        Assert.Equal("Processado", r.Status); Assert.Empty(await db.EstoquesEpi.ToListAsync());
    }

    [Fact] public async Task ConversaoFracionaria_NaoArredondaSilenciosamente()
    {
        var (db, s, _, _) = await Criar();
        var r = await s.ReceberAsync(Evento(qtd: 0.001m), default);
        Assert.Equal("Pendente", r.Status); Assert.Contains("inteira", r.Itens[0].Pendencia);
        Assert.Empty(await db.EstoquesEpi.ToListAsync());
    }

    [Fact] public async Task Uniforme_RespeitaTamanhoEObra()
    {
        var (db, s, _, _) = await Criar(); var u = new CatalogoUniforme { Nome = "Camisa" }; db.Add(u); await db.SaveChangesAsync();
        await s.VincularProdutoAsync(new("CAMISA-M", "UN", "UNIFORME", u.Id, "M", 1), default);
        await s.VincularProdutoAsync(new("CAMISA-G", "UN", "UNIFORME", u.Id, "G", 1), default);
        var e = Evento() with { Itens = [new("M", "CAMISA-M", "Camisa M", "UN", 10, null, false), new("G", "CAMISA-G", "Camisa G", "UN", 20, null, false)] };
        await s.ReceberAsync(e, default);
        Assert.Equal(10, (await db.EstoquesUniforme.SingleAsync(x => x.Tamanho == "M")).Saldo);
        Assert.Equal(20, (await db.EstoquesUniforme.SingleAsync(x => x.Tamanho == "G")).Saldo);
    }

    [Fact] public async Task Epc_UsaEstoqueCorrespondente()
    {
        var (db, s, _, _) = await Criar(); var epc = new CatalogoEpc { Nome = "Cone" }; db.Add(epc); await db.SaveChangesAsync();
        await s.VincularProdutoAsync(new("CONE", "UN", "EPC", epc.Id, null, 1), default);
        var e = Evento() with { Itens = [new("1", "CONE", "Cone", "UN", 7, null, false)] };
        await s.ReceberAsync(e, default);
        Assert.Equal(7, (await db.EstoquesEpc.SingleAsync()).Saldo); Assert.Empty(await db.EstoquesEpi.ToListAsync());
    }

    [Fact] public async Task RemocaoDeItemNaRevisao_EstornaQuantidadeAnterior()
    {
        var (db, s, _, _) = await Criar(); var e = Evento(); e.Itens.Add(e.Itens[0] with { ItemId = "2" });
        await s.ReceberAsync(e, default);
        await s.ReceberAsync(Evento("EV-2", versao: 2), default);
        Assert.Equal(200, (await db.EstoquesEpi.SingleAsync()).Saldo);
    }

    [Fact] public async Task MapeamentoNaoPodeMudarAposCadastro()
    {
        var (_, s, _, epi) = await Criar();
        await Assert.ThrowsAsync<ConflitoGsupriException>(() => s.VincularProdutoAsync(new("LUVA", "CX", "EPI", epi.Id, null, 10), default));
    }

    [Fact] public async Task NovaRevisaoPodeAcrescentarItemAoRecebimentoPersistido()
    {
        var (db, s, _, _) = await Criar(); await s.ReceberAsync(Evento(), default);
        var e = Evento("EV-2", versao: 2); e.Itens.Add(e.Itens[0] with { ItemId = "NOVO" });
        await s.ReceberAsync(e, default);
        Assert.Equal(400, (await db.EstoquesEpi.SingleAsync()).Saldo);
        Assert.Equal(2, await db.Set<GsupriRecebimentoItem>().CountAsync());
    }

    [Fact] public async Task CancelamentoAntesDoVinculo_NaoExigeConfiguracaoDesnecessaria()
    {
        var (db, s, _, _) = await Criar(false);
        await s.ReceberAsync(Evento(), default);
        var r = await s.ReceberAsync(Evento("EV-2", versao: 2) with { Cancelado = true, Itens = [] }, default);
        Assert.Equal("Cancelado", r.Status); Assert.Empty(await db.EstoquesEpi.ToListAsync());
    }

    [Fact] public async Task PerfilLimitadoNaoLeRecebimentosNemVincula()
    {
        var (db, _, obra, _) = await Criar(); var usuario = new CurrentUserService(); usuario.DefinirEscopo(false, [obra.Id]);
        var s = new IntegracaoGsupriService(db, usuario, new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.PainelAsync(1, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.ReceberAsync(Evento(), default));
    }

    [Fact] public async Task QuantidadesInvalidasEItensRepetidos_SaoRecusadosAntesDeSalvar()
    {
        var (db, s, _, _) = await Criar();
        await Assert.ThrowsAsync<ValidationException>(() => s.ReceberAsync(Evento(qtd: -1), default));
        var e = Evento(); e.Itens.Add(e.Itens[0]);
        await Assert.ThrowsAsync<ValidationException>(() => s.ReceberAsync(e, default));
        e = Evento() with { Itens = [Evento().Itens[0] with { QuantidadeLiberada = 3 }] };
        await Assert.ThrowsAsync<ValidationException>(() => s.ReceberAsync(e, default));
        Assert.Empty(await db.Set<GsupriEvento>().ToListAsync());
    }
}
