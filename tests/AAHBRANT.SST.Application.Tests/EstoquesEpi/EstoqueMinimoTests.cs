using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Application.EstoquesEpi.Commands;
using AAHBRANT.SST.Application.EstoquesEpi.Minimo;
using AAHBRANT.SST.Application.EstoquesEpi.Reposicao;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Application.Tests.PlanosIntegracao;
using AAHBRANT.SST.Application.Tests.Terceirizados;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EstoquesEpi;

// Especificação 21/10/2026, Módulo 2: mínimo por EPI e obra, sugestão, situação, alerta sem repetição,
// transferência entre obras e solicitação de reposição.
public class EstoqueMinimoTests
{
    private sealed class Cenario
    {
        public required SstDbContext Db { get; init; }
        public required Obra Obra { get; init; }
        public required Obra Outra { get; init; }
        public required Funcao Armador { get; init; }
        public required CatalogoEpi Luva { get; init; }
        public FilaNotificacaoTeamsFalsa Fila { get; } = new();
        public TecnicosSegurancaPorObraServiceFalso Tecnicos { get; } = new() { UsuarioIdsARetornar = { Guid.NewGuid(), Guid.NewGuid() } };
        public VerificacaoEstoqueMinimoService Verificacao => new(Db, Tecnicos, Fila);

        public async Task<Trabalhador> TrabalhadorAsync(DateTime? admissao = null)
        {
            var t = new Trabalhador { ObraId = Obra.Id, Funcao = Armador, FuncaoId = Armador.Id, Nome = "João", Cpf = "00000000000", DataAdmissao = admissao ?? new DateTime(2025, 1, 10) };
            Db.Trabalhadores.Add(t);
            await Db.SaveChangesAsync();
            return t;
        }

        public async Task<EstoqueEpiPainelItemDto> ItemAsync() =>
            (await CalculadoraEstoqueEpi.CalcularAsync(Db, Obra.Id, default)).Single(i => i.CatalogoEpiId == Luva.Id);

        public async Task<EstoqueEpi> EstoqueAsync(Obra obra) =>
            await Db.EstoquesEpi.SingleAsync(e => e.ObraId == obra.Id && e.CatalogoEpiId == Luva.Id);
    }

    private static async Task<Cenario> MontarAsync(int saldo = 20, int saldoOutra = 0, int prazo = 15)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "AUR", Nome = "Edifício Aurora", PrazoReposicaoEpiDias = prazo };
        var outra = new Obra { Codigo = "CUI", Nome = "Ponte Rio Cuiá" };
        var armador = new Funcao { Nome = "Armador" };
        var luva = new CatalogoEpi { Nome = "Luva de Vaqueta", VidaUtilEmMeses = 3, CertificadoAprovacaoNumero = "CA-12345" };
        db.Obras.AddRange(obra, outra);
        db.Funcoes.Add(armador);
        db.CatalogoEpis.Add(luva);
        db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao { FuncaoId = armador.Id, CatalogoEpiId = luva.Id });
        db.EstoquesEpi.AddRange(
            new EstoqueEpi { ObraId = obra.Id, CatalogoEpiId = luva.Id, Saldo = saldo },
            new EstoqueEpi { ObraId = outra.Id, CatalogoEpiId = luva.Id, Saldo = saldoOutra });
        await db.SaveChangesAsync();
        return new Cenario { Db = db, Obra = obra, Outra = outra, Armador = armador, Luva = luva };
    }

    [Theory]
    [InlineData(10, null, 0, "semMinimo")]
    [InlineData(0, null, 1, "zerado")]
    [InlineData(20, 10, 0, "emDia")]
    [InlineData(12, 10, 0, "atencao")]
    [InlineData(8, 10, 0, "abaixoMinimo")]
    [InlineData(0, 10, 0, "zerado")]
    [InlineData(0, 0, 0, "emDia")]
    public void Situacao_SegueAsFaixasDaEspecificacao(int disponivel, int? minimo, int emFalta, string esperado)
        => Assert.Equal(esperado, CalculadoraEstoqueEpi.Situacao(disponivel, minimo, emFalta));

    // Exemplo do documento: 45 saídas em 90 dias (0,5/dia) × 15 dias + segurança.
    [Fact]
    public async Task Sugestao_UsaConsumoDosUltimos90Dias_PrazoDaObra_E_Seguranca()
    {
        var c = await MontarAsync(saldo: 60);
        var estoque = await c.EstoqueAsync(c.Obra);
        c.Db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi { EstoqueEpiId = estoque.Id, Tipo = TipoMovimentacaoEstoqueEpi.SaidaEntrega, Quantidade = 45, SaldoResultante = 60 });
        await c.Db.SaveChangesAsync();
        for (var i = 0; i < 4; i++) await c.TrabalhadorAsync(DateTime.UtcNow.Date.AddDays(-5));

        var item = await c.ItemAsync();

        Assert.Equal(45, item.ConsumoUltimos90Dias);
        // 0,5 × 15 = 7,5 + 4 admissões recentes = 11,5 → 12.
        Assert.Equal(12, item.MinimoSugerido);
    }

    [Fact]
    public async Task Sugestao_SemHistorico_EstimaPelaVidaUtil()
    {
        var c = await MontarAsync();
        for (var i = 0; i < 9; i++) await c.TrabalhadorAsync();

        var item = await c.ItemAsync();

        // 9 pessoas ÷ (3 meses × 30) = 0,1/dia × 15 = 1,5 + segurança 2 = 3,5 → 4.
        Assert.Equal(4, item.MinimoSugerido);
    }

    [Fact]
    public async Task EpiQueNinguemUsa_TemSugestaoZero()
    {
        var c = await MontarAsync();
        Assert.Equal(0, (await c.ItemAsync()).MinimoSugerido);
    }

    [Fact]
    public async Task Disponivel_DescontaKitsSeparados_E_CompraRepoeAteODobroDoMinimo()
    {
        var c = await MontarAsync(saldo: 6);
        var t = await c.TrabalhadorAsync();
        c.Db.ReservasEpi.Add(new ReservaEpi { TrabalhadorId = t.Id, ObraId = c.Obra.Id, CatalogoEpiId = c.Luva.Id, Quantidade = 2 });
        var estoque = await c.EstoqueAsync(c.Obra);
        estoque.EstoqueMinimo = 5;
        await c.Db.SaveChangesAsync();

        var item = await c.ItemAsync();

        Assert.Equal(4, item.Disponivel);
        Assert.Equal(SituacaoEstoqueEpi.AbaixoMinimo, item.Situacao);
        Assert.Equal(6, item.QuantidadeSugeridaCompra);
    }

    [Fact]
    public async Task OutraObraComSobra_ApareceComoSugestaoDeTransferencia()
    {
        var c = await MontarAsync(saldo: 1, saldoOutra: 30);
        (await c.EstoqueAsync(c.Obra)).EstoqueMinimo = 5;
        (await c.EstoqueAsync(c.Outra)).EstoqueMinimo = 10;
        await c.Db.SaveChangesAsync();

        var item = await c.ItemAsync();

        var sugestao = Assert.Single(item.Transferencias);
        Assert.Equal(c.Outra.Id, sugestao.ObraId);
        // Falta 2×5−1 = 9; a outra obra guarda 1,5× o próprio mínimo (15) e tem sobra de 15.
        Assert.Equal(9, sugestao.Quantidade);
    }

    [Fact]
    public async Task AbaixoDoMinimo_AvisaOsTecnicosUmaVezSo_E_EncerraQuandoVoltaAoNormal()
    {
        var c = await MontarAsync(saldo: 3);
        (await c.EstoqueAsync(c.Obra)).EstoqueMinimo = 5;
        await c.Db.SaveChangesAsync();

        await c.Verificacao.VerificarAsync(c.Obra.Id);
        await c.Verificacao.VerificarAsync(c.Obra.Id);

        var alertas = await c.Db.Alertas.Where(a => a.Tipo == TipoAlerta.EstoqueEpiAbaixoMinimo).ToListAsync();
        Assert.Equal(2, alertas.Count);
        Assert.Equal(2, c.Fila.Mensagens.Count);
        Assert.Contains("Luva de Vaqueta", alertas[0].Titulo);

        await new RegistrarEntradaEstoqueEpiCommandHandler(c.Db, c.Verificacao)
            .Handle(new RegistrarEntradaEstoqueEpiCommand(c.Luva.Id, c.Obra.Id, 10, "Compra"), default);

        Assert.All(await c.Db.Alertas.ToListAsync(), a => Assert.Equal(StatusAlerta.Resolvido, a.Status));
    }

    [Fact]
    public async Task EntregaQueDeixaAbaixoDoMinimo_DisparaOAlertaNaHora()
    {
        var c = await MontarAsync(saldo: 5);
        (await c.EstoqueAsync(c.Obra)).EstoqueMinimo = 5;
        await c.Db.SaveChangesAsync();
        var t = await c.TrabalhadorAsync();

        await new CriarEntregaEpiCommandHandler(c.Db, c.Verificacao).Handle(
            new CriarEntregaEpiCommand(t.Id, c.Luva.Id, DateTime.UtcNow.Date, null, null, 1, null, null, null, MotivoEntregaEpi.Inicial, null, null), default);

        Assert.Equal(2, await c.Db.Alertas.CountAsync(a => a.Tipo == TipoAlerta.EstoqueEpiAbaixoMinimo));
    }

    [Fact]
    public async Task AplicarSugestoes_SoPreencheQuemNaoTemMinimo()
    {
        var c = await MontarAsync();
        for (var i = 0; i < 9; i++) await c.TrabalhadorAsync();

        var aplicados = await new AplicarSugestoesEstoqueMinimoCommandHandler(c.Db).Handle(new AplicarSugestoesEstoqueMinimoCommand(c.Obra.Id), default);

        Assert.Equal(1, aplicados);
        Assert.Equal(4, (await c.EstoqueAsync(c.Obra)).EstoqueMinimo);
        Assert.Equal(0, await new AplicarSugestoesEstoqueMinimoCommandHandler(c.Db).Handle(new AplicarSugestoesEstoqueMinimoCommand(c.Obra.Id), default));
    }

    [Fact]
    public async Task Transferencia_MoveOSaldo_E_RegistraOsDoisLados()
    {
        var c = await MontarAsync(saldo: 2, saldoOutra: 30);

        await new TransferirEstoqueEpiCommandHandler(c.Db).Handle(new TransferirEstoqueEpiCommand(c.Luva.Id, c.Outra.Id, c.Obra.Id, 8, "Reforço"), default);

        Assert.Equal(10, (await c.EstoqueAsync(c.Obra)).Saldo);
        Assert.Equal(22, (await c.EstoqueAsync(c.Outra)).Saldo);
        var movimentos = await c.Db.MovimentacoesEstoqueEpi.ToListAsync();
        Assert.Contains(movimentos, m => m.Tipo == TipoMovimentacaoEstoqueEpi.TransferenciaSaida && m.Observacao!.Contains("Edifício Aurora"));
        Assert.Contains(movimentos, m => m.Tipo == TipoMovimentacaoEstoqueEpi.TransferenciaEntrada && m.Observacao!.Contains("Ponte Rio Cuiá"));
    }

    [Fact]
    public async Task Transferencia_NaoLevaUnidadeSeparadaParaKit()
    {
        var c = await MontarAsync(saldo: 3);
        var t = await c.TrabalhadorAsync();
        c.Db.ReservasEpi.Add(new ReservaEpi { TrabalhadorId = t.Id, ObraId = c.Obra.Id, CatalogoEpiId = c.Luva.Id, Quantidade = 2 });
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new TransferirEstoqueEpiCommandHandler(c.Db).Handle(new TransferirEstoqueEpiCommand(c.Luva.Id, c.Obra.Id, c.Outra.Id, 2, null), default));
    }

    [Fact]
    public async Task SolicitacaoDeReposicao_GuardaORetratoDosItensAbaixoDoMinimo()
    {
        var c = await MontarAsync(saldo: 3);
        (await c.EstoqueAsync(c.Obra)).EstoqueMinimo = 5;
        await c.Db.SaveChangesAsync();

        // Sem usuário vinculado não há assinatura — mediator e registrador não são usados.
        var id = await new CriarSolicitacaoReposicaoEpiCommandHandler(c.Db, new GeradorNumeroSequencialFalso(), null!, null!)
            .Handle(new CriarSolicitacaoReposicaoEpiCommand(c.Obra.Id, "Urgente"), default);

        var s = await c.Db.SolicitacoesReposicaoEpi.Include(x => x.Itens).SingleAsync(x => x.Id == id);
        Assert.StartsWith("REP-EPI-", s.NumeroDocumento);
        var item = Assert.Single(s.Itens);
        Assert.Equal("CA-12345", item.CertificadoAprovacaoNumero);
        Assert.Equal(7, item.QuantidadeSugerida);
        Assert.Equal(15, s.PrazoReposicaoDias);
    }

    [Fact]
    public async Task SolicitacaoDeReposicao_SemNadaAbaixoDoMinimo_EhRecusada()
    {
        var c = await MontarAsync(saldo: 50);
        (await c.EstoqueAsync(c.Obra)).EstoqueMinimo = 5;
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CriarSolicitacaoReposicaoEpiCommandHandler(c.Db, new GeradorNumeroSequencialFalso(), null!, null!)
                .Handle(new CriarSolicitacaoReposicaoEpiCommand(c.Obra.Id, null), default));
    }
}
