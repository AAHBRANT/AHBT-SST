using AAHBRANT.SST.Application.EstoquesEpi.Commands;
using AAHBRANT.SST.Application.EstoquesEpi.Minimo;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EstoquesEpi;

// Lançamento de estoque exige obra com acesso explícito do usuário (entrada, ajuste e as duas pontas
// da transferência), e a transferência de correção exige motivo e fica identificável na auditoria.
public class EstoqueObraContextoTests
{
    private sealed record Cenario(SstDbContext Global, SstDbContext Restrito, Obra Permitida, Obra Negada, CatalogoEpi Luva);

    private static async Task<Cenario> MontarAsync()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var global = new SstDbContext(opcoes, new CurrentUserService());
        var permitida = new Obra { Codigo = "CUI", Nome = "Cuiá" };
        var negada = new Obra { Codigo = "PRO", Nome = "Parque Roger" };
        var luva = new CatalogoEpi { Nome = "Luva", CertificadoAprovacaoNumero = "CA-1" };
        global.Obras.AddRange(permitida, negada);
        global.CatalogoEpis.Add(luva);
        global.EstoquesEpi.AddRange(
            new EstoqueEpi { ObraId = permitida.Id, CatalogoEpiId = luva.Id, Saldo = 10 },
            new EstoqueEpi { ObraId = negada.Id, CatalogoEpiId = luva.Id, Saldo = 10 });
        await global.SaveChangesAsync();

        var usuario = new CurrentUserService();
        usuario.DefinirEscopo(false, new[] { permitida.Id });
        return new Cenario(global, new SstDbContext(opcoes, usuario), permitida, negada, luva);
    }

    [Fact]
    public async Task Entrada_EmObraSemAcesso_E_Recusada_ESemEfeito()
    {
        var c = await MontarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new RegistrarEntradaEstoqueEpiCommandHandler(c.Restrito).Handle(new RegistrarEntradaEstoqueEpiCommand(c.Luva.Id, c.Negada.Id, 5, null), default));

        Assert.Equal(10, (await c.Global.EstoquesEpi.SingleAsync(e => e.ObraId == c.Negada.Id)).Saldo);
    }

    [Fact]
    public async Task Entrada_EmObraComAcesso_Funciona()
    {
        var c = await MontarAsync();

        await new RegistrarEntradaEstoqueEpiCommandHandler(c.Restrito).Handle(new RegistrarEntradaEstoqueEpiCommand(c.Luva.Id, c.Permitida.Id, 5, null), default);

        Assert.Equal(15, (await c.Global.EstoquesEpi.SingleAsync(e => e.ObraId == c.Permitida.Id)).Saldo);
    }

    [Fact]
    public async Task Ajuste_EmObraSemAcesso_E_Recusado()
    {
        var c = await MontarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new AjustarEstoqueEpiCommandHandler(c.Restrito).Handle(new AjustarEstoqueEpiCommand(c.Luva.Id, c.Negada.Id, 0, "teste"), default));
    }

    [Fact]
    public async Task Transferencia_ParaObraSemAcesso_E_Recusada_ESemEfeito()
    {
        var c = await MontarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new TransferirEstoqueEpiCommandHandler(c.Restrito).Handle(new TransferirEstoqueEpiCommand(c.Luva.Id, c.Permitida.Id, c.Negada.Id, 3, null), default));

        Assert.Equal(10, (await c.Global.EstoquesEpi.SingleAsync(e => e.ObraId == c.Permitida.Id)).Saldo);
        Assert.Equal(10, (await c.Global.EstoquesEpi.SingleAsync(e => e.ObraId == c.Negada.Id)).Saldo);
        Assert.Empty(await c.Global.MovimentacoesEstoqueEpi.ToListAsync());
    }

    [Fact]
    public async Task Correcao_ExigeMotivo()
    {
        var validador = new TransferirEstoqueEpiCommandValidator();
        var semMotivo = new TransferirEstoqueEpiCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "  ", Correcao: true);
        var comMotivo = semMotivo with { Observacao = "Lançado na obra errada" };
        var transferenciaComum = semMotivo with { Observacao = null, Correcao = false };

        Assert.False((await validador.ValidateAsync(semMotivo)).IsValid);
        Assert.True((await validador.ValidateAsync(comMotivo)).IsValid);
        Assert.True((await validador.ValidateAsync(transferenciaComum)).IsValid);
    }

    [Fact]
    public async Task Correcao_RegistraTiposDeCorrecao_ComOMesmoCodigoNasDuasPontas()
    {
        var c = await MontarAsync();

        await new TransferirEstoqueEpiCommandHandler(c.Global).Handle(
            new TransferirEstoqueEpiCommand(c.Luva.Id, c.Negada.Id, c.Permitida.Id, 4, "Lançado na obra errada", Correcao: true), default);

        var movimentos = await c.Global.MovimentacoesEstoqueEpi.ToListAsync();
        var saida = Assert.Single(movimentos, m => m.Tipo == TipoMovimentacaoEstoqueEpi.CorrecaoLancamentoSaida);
        var entrada = Assert.Single(movimentos, m => m.Tipo == TipoMovimentacaoEstoqueEpi.CorrecaoLancamentoEntrada);
        Assert.Contains("Cuiá", saida.Observacao);
        Assert.Contains("Parque Roger", entrada.Observacao);
        Assert.Contains("Lançado na obra errada", saida.Observacao);

        string Codigo(string s) => s.Split(' ').Single(p => p.StartsWith("TRF-"));
        Assert.Equal(Codigo(saida.Observacao!), Codigo(entrada.Observacao!));
        Assert.Equal(6, (await c.Global.EstoquesEpi.SingleAsync(e => e.ObraId == c.Negada.Id)).Saldo);
        Assert.Equal(14, (await c.Global.EstoquesEpi.SingleAsync(e => e.ObraId == c.Permitida.Id)).Saldo);
    }
}
