using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Application.PlanosIntegracao;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Application.Tests.Terceirizados;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.PlanosIntegracao;

// Especificação 21/10/2026, Módulo 1: funcionário que entra na obra já tem o kit da função separado,
// um plano de integração aberto e os técnicos da obra avisados no Teams.
public class PlanoIntegracaoServiceTests
{
    private sealed class Cenario
    {
        public required SstDbContext Db { get; init; }
        public required Obra Obra { get; init; }
        public required Funcao Armador { get; init; }
        public required Funcao Servente { get; init; }
        public required CatalogoEpi Capacete { get; init; }
        public required CatalogoEpi Bota { get; init; }
        public required CatalogoEpi Luva { get; init; }
        public FilaNotificacaoTeamsFalsa Fila { get; } = new();
        public TecnicosSegurancaPorObraServiceFalso Tecnicos { get; } = new() { UsuarioIdsARetornar = { Guid.NewGuid(), Guid.NewGuid() } };
        public FilaCalendarioTeamsFalsa Calendario { get; } = new();
        public PlanoIntegracaoService Servico => new(Db, Tecnicos, Fila, Calendario, new GeradorNumeroSequencialFalso());

        public async Task<Trabalhador> TrabalhadorAsync(Funcao funcao, string nome = "João da Silva", string? calcado = "42")
        {
            var t = new Trabalhador
            {
                ObraId = Obra.Id, FuncaoId = funcao.Id, Nome = nome, Cpf = "00000000000",
                DataAdmissao = DateTime.UtcNow.Date, TamanhoCalcadoEpi = calcado,
            };
            Db.Trabalhadores.Add(t);
            await Db.SaveChangesAsync();
            return t;
        }

        public async Task<int> SaldoAsync(CatalogoEpi epi) =>
            (await Db.EstoquesEpi.SingleAsync(e => e.CatalogoEpiId == epi.Id && e.ObraId == Obra.Id)).Saldo;
    }

    // Armador: capacete + bota. Servente: capacete + luva. Saldo: capacete 5, bota 1, luva 0.
    private static async Task<Cenario> MontarAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "AUR", Nome = "Edifício Aurora" };
        var armador = new Funcao { Nome = "Armador" };
        var servente = new Funcao { Nome = "Servente" };
        var capacete = new CatalogoEpi { Nome = "Capacete classe B", VidaUtilEmMeses = 24 };
        var bota = new CatalogoEpi { Nome = "Bota de Segurança com Bico de Aço", VidaUtilEmMeses = 12 };
        var luva = new CatalogoEpi { Nome = "Luva de Vaqueta", VidaUtilEmMeses = 3 };
        db.Obras.Add(obra);
        db.Funcoes.AddRange(armador, servente);
        db.CatalogoEpis.AddRange(capacete, bota, luva);
        db.MatrizEpiFuncoes.AddRange(
            new MatrizEpiFuncao { FuncaoId = armador.Id, CatalogoEpiId = capacete.Id },
            new MatrizEpiFuncao { FuncaoId = armador.Id, CatalogoEpiId = bota.Id },
            new MatrizEpiFuncao { FuncaoId = servente.Id, CatalogoEpiId = capacete.Id },
            new MatrizEpiFuncao { FuncaoId = servente.Id, CatalogoEpiId = luva.Id });
        db.EstoquesEpi.AddRange(
            new EstoqueEpi { ObraId = obra.Id, CatalogoEpiId = capacete.Id, Saldo = 5 },
            new EstoqueEpi { ObraId = obra.Id, CatalogoEpiId = bota.Id, Saldo = 1 },
            new EstoqueEpi { ObraId = obra.Id, CatalogoEpiId = luva.Id, Saldo = 0 });
        await db.SaveChangesAsync();
        return new Cenario { Db = db, Obra = obra, Armador = armador, Servente = servente, Capacete = capacete, Bota = bota, Luva = luva };
    }

    private static CriarEntregaEpiCommand Entrega(Trabalhador t, CatalogoEpi epi) =>
        new(t.Id, epi.Id, DateTime.UtcNow.Date, null, null, 1, null, null, null, MotivoEntregaEpi.Inicial, null, null);

    [Fact]
    public async Task Admissao_SeparaOKitDaFuncao_AbrePlano_E_AvisaOsTecnicos()
    {
        var c = await MontarAsync();
        var joao = await c.TrabalhadorAsync(c.Armador);

        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.Admissao);

        var reservas = await c.Db.ReservasEpi.Where(r => r.TrabalhadorId == joao.Id).ToListAsync();
        Assert.Equal(2, reservas.Count);
        Assert.All(reservas, r => Assert.Equal(StatusReservaEpi.Ativa, r.Status));
        Assert.Equal("42", reservas.Single(r => r.CatalogoEpiId == c.Bota.Id).Tamanho);
        Assert.Null(reservas.Single(r => r.CatalogoEpiId == c.Capacete.Id).Tamanho);

        // Separar não baixa o estoque físico.
        Assert.Equal(5, await c.SaldoAsync(c.Capacete));

        var plano = await c.Db.PlanosIntegracao.SingleAsync();
        Assert.Equal(StatusPlanoIntegracao.Aberto, plano.Status);
        Assert.Equal(MotivoPlanoIntegracao.Admissao, plano.Motivo);

        var alertas = await c.Db.Alertas.Where(a => a.Tipo == TipoAlerta.IntegracaoFuncionarioNovo).ToListAsync();
        Assert.Equal(2, alertas.Count);
        Assert.Contains("João da Silva", alertas[0].Titulo);
        Assert.Contains("2 item(ns) separado(s)", alertas[0].Descricao);
        Assert.Equal(2, c.Fila.Mensagens.Count);
    }

    [Fact]
    public async Task ItemSemSaldo_FicaEmFalta_E_OAvisoDizOQueFalta()
    {
        var c = await MontarAsync();
        var maria = await c.TrabalhadorAsync(c.Servente, "Maria Souza");

        await c.Servico.SincronizarAsync(maria.Id, MotivoPlanoIntegracao.Admissao);

        var alerta = await c.Db.Alertas.FirstAsync();
        Assert.Contains("1 separado(s), 1 em falta (Luva de Vaqueta)", alerta.Descricao);
        Assert.Equal(SeveridadeAlerta.Critico, alerta.Severidade);
    }

    // A fila respeita a ordem: com uma bota só, quem chegou primeiro fica com ela.
    [Fact]
    public async Task DoisFuncionarios_UmaBota_PrimeiroSeparado_SegundoEmFalta()
    {
        var c = await MontarAsync();
        var primeiro = await c.TrabalhadorAsync(c.Armador, "Primeiro");
        await c.Servico.SincronizarAsync(primeiro.Id, MotivoPlanoIntegracao.Admissao);
        var segundo = await c.TrabalhadorAsync(c.Armador, "Segundo");
        await c.Servico.SincronizarAsync(segundo.Id, MotivoPlanoIntegracao.Admissao);

        var reservasBota = await c.Db.ReservasEpi.Where(r => r.CatalogoEpiId == c.Bota.Id).ToListAsync();
        var separadas = await SituacaoReservasEpi.SeparadasAsync(c.Db, c.Obra.Id, new[] { c.Bota.Id }, default);

        Assert.True(separadas[reservasBota.Single(r => r.TrabalhadorId == primeiro.Id).Id]);
        Assert.False(separadas[reservasBota.Single(r => r.TrabalhadorId == segundo.Id).Id]);
    }

    [Fact]
    public async Task EntregaAvulsa_NaoPodeUsarAUnidadeSeparadaParaOutro()
    {
        var c = await MontarAsync();
        var joao = await c.TrabalhadorAsync(c.Armador);
        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.Admissao);
        var outro = await c.TrabalhadorAsync(c.Servente, "Outro");
        await Nr6DeTeste.SemearAsync(c.Db, outro.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CriarEntregaEpiCommandHandler(c.Db).Handle(Entrega(outro, c.Bota), default));

        Assert.Contains("separados para kits de funcionários novos", ex.Message);
        Assert.Equal(1, await c.SaldoAsync(c.Bota));
    }

    [Fact]
    public async Task EntregaParaOProprio_UsaAReserva_E_BaixaOEstoque()
    {
        var c = await MontarAsync();
        var joao = await c.TrabalhadorAsync(c.Armador);
        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.Admissao);
        await Nr6DeTeste.SemearAsync(c.Db, joao.Id);

        var entregaId = await new CriarEntregaEpiCommandHandler(c.Db).Handle(Entrega(joao, c.Bota), default);

        var reserva = await c.Db.ReservasEpi.SingleAsync(r => r.CatalogoEpiId == c.Bota.Id);
        Assert.Equal(StatusReservaEpi.Entregue, reserva.Status);
        Assert.Equal(entregaId, reserva.EntregaEpiId);
        Assert.Equal(0, await c.SaldoAsync(c.Bota));
    }

    [Fact]
    public async Task ExcluirAEntrega_VoltaASepararOItem()
    {
        var c = await MontarAsync();
        var joao = await c.TrabalhadorAsync(c.Armador);
        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.Admissao);
        await Nr6DeTeste.SemearAsync(c.Db, joao.Id);
        var entregaId = await new CriarEntregaEpiCommandHandler(c.Db).Handle(Entrega(joao, c.Bota), default);

        await new ExcluirEntregaEpiCommandHandler(c.Db).Handle(new ExcluirEntregaEpiCommand(entregaId), default);

        var reserva = await c.Db.ReservasEpi.SingleAsync(r => r.CatalogoEpiId == c.Bota.Id);
        Assert.Equal(StatusReservaEpi.Ativa, reserva.Status);
        Assert.Null(reserva.EntregaEpiId);
        Assert.Equal(1, await c.SaldoAsync(c.Bota));
    }

    [Fact]
    public async Task TrocaDeFuncao_CancelaOQueNaoServeMais_SeparaONovo_E_ReabrePlano()
    {
        var c = await MontarAsync();
        var joao = await c.TrabalhadorAsync(c.Armador);
        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.Admissao);

        joao.FuncaoId = c.Servente.Id;
        await c.Db.SaveChangesAsync();
        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.TrocaFuncao);

        var reservas = await c.Db.ReservasEpi.ToListAsync();
        Assert.Equal(StatusReservaEpi.Cancelada, reservas.Single(r => r.CatalogoEpiId == c.Bota.Id).Status);
        Assert.Equal(StatusReservaEpi.Ativa, reservas.Single(r => r.CatalogoEpiId == c.Capacete.Id).Status);
        Assert.Equal(StatusReservaEpi.Ativa, reservas.Single(r => r.CatalogoEpiId == c.Luva.Id).Status);

        var planos = await c.Db.PlanosIntegracao.ToListAsync();
        Assert.Equal(StatusPlanoIntegracao.Cancelado, planos.Single(p => p.Motivo == MotivoPlanoIntegracao.Admissao).Status);
        Assert.Equal(StatusPlanoIntegracao.Aberto, planos.Single(p => p.Motivo == MotivoPlanoIntegracao.TrocaFuncao).Status);
    }

    [Fact]
    public async Task Desligamento_DevolveOKitAoDisponivel_E_CancelaOPlano()
    {
        var c = await MontarAsync();
        var joao = await c.TrabalhadorAsync(c.Armador);
        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.Admissao);

        joao.Situacao = SituacaoTrabalhador.Desligado;
        await c.Db.SaveChangesAsync();
        await c.Servico.SincronizarAsync(joao.Id, null);

        Assert.All(await c.Db.ReservasEpi.ToListAsync(), r => Assert.Equal(StatusReservaEpi.Cancelada, r.Status));
        Assert.Equal(StatusPlanoIntegracao.Cancelado, (await c.Db.PlanosIntegracao.SingleAsync()).Status);
        Assert.Equal(0, await SituacaoReservasEpi.ReservadoParaOutrosAsync(c.Db, c.Obra.Id, c.Bota.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task QuemJaEstaComOEpi_NaoGanhaReservaDele()
    {
        var c = await MontarAsync();
        var joao = await c.TrabalhadorAsync(c.Armador);
        c.Db.EntregasEpi.Add(new EntregaEpi { TrabalhadorId = joao.Id, CatalogoEpiId = c.Capacete.Id, DataEntrega = DateTime.UtcNow, Quantidade = 1 });
        await c.Db.SaveChangesAsync();

        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.Admissao);

        var reserva = Assert.Single(await c.Db.ReservasEpi.ToListAsync());
        Assert.Equal(c.Bota.Id, reserva.CatalogoEpiId);
    }

    // --- Gatilho pelo G-RH: só reage ao que mudou de verdade ---

    private static SincronizarColaboradorGrhCommand Grh(string cargo, DateTime admissao, SituacaoTrabalhador situacao = SituacaoTrabalhador.Ativo) => new(
        "38062559890", "Adriano Manoel da Silva", null, null, null, null, null, null, null, null, null,
        admissao, null, cargo, null, null, situacao, "Edifício Aurora", null, null, null, null, "41");

    private static SincronizarColaboradorGrhCommandHandler HandlerGrh(Cenario c) =>
        new(c.Db, new CpfHashService(), planoIntegracao: c.Servico);

    [Fact]
    public async Task Grh_ColaboradorNovo_AbrePlano_E_RepetirACarga_NaoAbreOutro()
    {
        var c = await MontarAsync();
        var handler = HandlerGrh(c);

        await handler.Handle(Grh("Armador", DateTime.UtcNow.Date), default);
        await handler.Handle(Grh("Armador", DateTime.UtcNow.Date), default);

        Assert.Single(await c.Db.PlanosIntegracao.ToListAsync());
        Assert.Equal(2, await c.Db.ReservasEpi.CountAsync());
        Assert.Equal(2, c.Fila.Mensagens.Count);
    }

    // Carga inicial de obra nova traz quem foi admitido há anos: não é funcionário novo.
    [Fact]
    public async Task Grh_ColaboradorAntigo_NaoAbrePlano()
    {
        var c = await MontarAsync();

        await HandlerGrh(c).Handle(Grh("Armador", new DateTime(2020, 3, 2)), default);

        Assert.Empty(await c.Db.PlanosIntegracao.ToListAsync());
        Assert.Empty(await c.Db.ReservasEpi.ToListAsync());
        Assert.Empty(c.Fila.Mensagens);
    }

    [Fact]
    public async Task Grh_TrocaDeCargo_AbrePlanoDeTrocaDeFuncao()
    {
        var c = await MontarAsync();
        var handler = HandlerGrh(c);
        await handler.Handle(Grh("Armador", new DateTime(2020, 3, 2)), default);

        await handler.Handle(Grh("Servente", new DateTime(2020, 3, 2)), default);

        var plano = await c.Db.PlanosIntegracao.SingleAsync();
        Assert.Equal(MotivoPlanoIntegracao.TrocaFuncao, plano.Motivo);
    }
}
