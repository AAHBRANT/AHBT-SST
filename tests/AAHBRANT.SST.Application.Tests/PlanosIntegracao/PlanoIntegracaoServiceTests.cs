using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Application.Integracoes;
using AAHBRANT.SST.Application.PlanosIntegracao;
using AAHBRANT.SST.Application.Terceirizados.Commands;
using Microsoft.Extensions.Options;
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

        public PlanoIntegracaoService ServicoCom(OpcoesIntegracao opcoes) =>
            new(Db, Tecnicos, Fila, Calendario, new GeradorNumeroSequencialFalso(), opcoes: Options.Create(opcoes));

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
        // Aviso agrupado por obra/dia: o título traz o contador; o nome vai na descrição.
        Assert.Equal("1 pessoa entrou na obra Edifício Aurora hoje", alertas[0].Titulo);
        Assert.Contains("João da Silva", alertas[0].Descricao);
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

    // --- Aviso agrupado por obra/dia ---

    [Fact]
    public async Task DuasFichasNoMesmoDia_UmAlertaPorTecnico_ComContador2_E_UmEnvioAoTeamsPorTecnico()
    {
        var c = await MontarAsync();
        var primeiro = await c.TrabalhadorAsync(c.Armador, "Primeiro");
        var segundo = await c.TrabalhadorAsync(c.Servente, "Segundo");

        await c.Servico.SincronizarAsync(primeiro.Id, MotivoPlanoIntegracao.Admissao);
        await c.Servico.SincronizarAsync(segundo.Id, MotivoPlanoIntegracao.Admissao);

        var alertas = await c.Db.Alertas.Where(a => a.Tipo == TipoAlerta.IntegracaoFuncionarioNovo).ToListAsync();
        Assert.Equal(2, alertas.Count); // 2 técnicos
        Assert.Equal(c.Tecnicos.UsuarioIdsARetornar.Count, alertas.Select(a => a.DestinatarioUsuarioId).Distinct().Count());
        Assert.All(alertas, a =>
        {
            Assert.Equal("2 pessoas entraram na obra Edifício Aurora hoje", a.Titulo);
            Assert.Contains("Primeiro", a.Descricao);
            Assert.Contains("Segundo", a.Descricao);
            Assert.Equal(PlanoIntegracaoService.OrigemAvisoEntradasObra, a.EntidadeOrigemTipo);
            Assert.Equal(c.Obra.Id, a.ObraId);
            Assert.Equal(StatusAlerta.Aberto, a.Status);
        });
        // Chave de dedup única por técnico: o Motor de Alertas indexa os em aberto por (origem tipo, origem id).
        Assert.Equal(2, alertas.Select(a => a.EntidadeOrigemId).Distinct().Count());
        // Só a criação do alerta do dia envia ao Teams: 1 mensagem por técnico, não 1 por ficha.
        Assert.Equal(2, c.Fila.Mensagens.Count);
        Assert.Empty(c.Calendario.Mensagens);
    }

    [Fact]
    public async Task AlertaAgrupado_AtualizaSeveridade_QuandoUmaFichaNovaTemEpiEmFalta()
    {
        var c = await MontarAsync();
        var armador = await c.TrabalhadorAsync(c.Armador, "Armador Um");
        await c.Servico.SincronizarAsync(armador.Id, MotivoPlanoIntegracao.Admissao);
        Assert.All(await c.Db.Alertas.ToListAsync(), a => Assert.Equal(SeveridadeAlerta.Atencao, a.Severidade));
        var servente = await c.TrabalhadorAsync(c.Servente, "Servente Dois"); // luva sem saldo

        await c.Servico.SincronizarAsync(servente.Id, MotivoPlanoIntegracao.Admissao);

        var alertas = await c.Db.Alertas.ToListAsync();
        Assert.All(alertas, a =>
        {
            Assert.Equal(SeveridadeAlerta.Critico, a.Severidade);
            Assert.Contains("1 em falta (Luva de Vaqueta)", a.Descricao);
        });
    }

    // Quem trata o alerta do dia (resolve/ignora) tira ele da lista; a próxima ficha abre um alerta novo.
    [Fact]
    public async Task AlertaResolvido_ProximaFichaDoDia_AbreAlertaNovo_EEnviaAoTeams()
    {
        var c = await MontarAsync();
        await c.Servico.SincronizarAsync((await c.TrabalhadorAsync(c.Armador, "Primeiro")).Id, MotivoPlanoIntegracao.Admissao);
        foreach (var a in await c.Db.Alertas.ToListAsync()) a.Status = StatusAlerta.Resolvido;
        await c.Db.SaveChangesAsync();

        await c.Servico.SincronizarAsync((await c.TrabalhadorAsync(c.Armador, "Segundo")).Id, MotivoPlanoIntegracao.Admissao);

        var abertos = await c.Db.Alertas.Where(a => a.Status == StatusAlerta.Aberto).ToListAsync();
        Assert.Equal(2, abertos.Count);
        Assert.Equal(4, c.Fila.Mensagens.Count);
    }

    // --- Prestador PJ e terceirizado ---

    [Fact]
    public async Task PrestadorPj_NaoGeraFicha_NemKit_NemAviso()
    {
        var c = await MontarAsync();
        var pj = await c.TrabalhadorAsync(c.Armador, "Prestador PJ");
        pj.Vinculo = TipoVinculo.PrestadorServico;
        await c.Db.SaveChangesAsync();

        await c.Servico.SincronizarAsync(pj.Id, MotivoPlanoIntegracao.Admissao);

        Assert.Empty(await c.Db.PlanosIntegracao.ToListAsync());
        Assert.Empty(await c.Db.ReservasEpi.ToListAsync());
        Assert.Empty(await c.Db.Alertas.ToListAsync());
    }

    [Fact]
    public async Task Terceirizado_Admissao_AbreFicha_SemReservarKit()
    {
        var c = await MontarAsync();
        var terceirizado = await c.TrabalhadorAsync(c.Armador, "Terceirizado");
        terceirizado.Vinculo = TipoVinculo.Terceirizado;
        await c.Db.SaveChangesAsync();

        await c.Servico.SincronizarAsync(terceirizado.Id, MotivoPlanoIntegracao.Admissao);

        Assert.Single(await c.Db.PlanosIntegracao.ToListAsync());
        Assert.Empty(await c.Db.ReservasEpi.ToListAsync());
    }

    // --- Troca de função / transferência com pacote válido ---

    private static async Task DarTreinamentosAsync(Cenario c, Trabalhador t, bool nr06 = true, bool nr18 = true, bool nr35 = true, int validadeEmDias = 365)
    {
        foreach (var norma in new[] { (nr06, "NR-06"), (nr18, "NR-18"), (nr35, "NR-35") }.Where(n => n.Item1).Select(n => n.Item2))
        {
            var curso = new CursoTreinamento { Nome = norma, NormaReferencia = norma, AtendeNr6 = norma == "NR-06" };
            c.Db.CursosTreinamento.Add(curso);
            c.Db.Treinamentos.Add(new Treinamento
            {
                TrabalhadorId = t.Id, CursoTreinamentoId = curso.Id, DataRealizacao = DateTime.UtcNow.Date.AddDays(-30),
                DataValidade = DateTime.UtcNow.Date.AddDays(validadeEmDias), CargaHorariaRealizada = 4,
            });
        }
        await c.Db.SaveChangesAsync();
    }

    private static async Task<Trabalhador> AdmitidoEDepoisTrocadoAsync(Cenario c, Action<Trabalhador>? antesDaTroca = null)
    {
        var joao = await c.TrabalhadorAsync(c.Armador);
        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.Admissao);
        joao.FuncaoId = c.Servente.Id;
        antesDaTroca?.Invoke(joao);
        await c.Db.SaveChangesAsync();
        return joao;
    }

    [Theory]
    [InlineData(MotivoPlanoIntegracao.TrocaFuncao)]
    [InlineData(MotivoPlanoIntegracao.TransferenciaObra)]
    public async Task ComPacoteValido_NaoAbreFichaNova_MasReajustaOKit_ENaoAvisaDeNovo(MotivoPlanoIntegracao motivo)
    {
        var c = await MontarAsync();
        var joao = await AdmitidoEDepoisTrocadoAsync(c);
        await DarTreinamentosAsync(c, joao);
        var alertasAntes = await c.Db.Alertas.CountAsync();
        var mensagensAntes = c.Fila.Mensagens.Count;

        await c.Servico.SincronizarAsync(joao.Id, motivo);

        var planos = await c.Db.PlanosIntegracao.ToListAsync();
        var admissao = Assert.Single(planos); // só a da admissão, ainda aberta
        Assert.Equal(MotivoPlanoIntegracao.Admissao, admissao.Motivo);
        Assert.Equal(StatusPlanoIntegracao.Aberto, admissao.Status);
        // Kit reajustado para a função nova (bota cancelada, luva separada).
        var reservas = await c.Db.ReservasEpi.ToListAsync();
        Assert.Equal(StatusReservaEpi.Cancelada, reservas.Single(r => r.CatalogoEpiId == c.Bota.Id).Status);
        Assert.Equal(StatusReservaEpi.Ativa, reservas.Single(r => r.CatalogoEpiId == c.Luva.Id).Status);
        Assert.Equal(alertasAntes, await c.Db.Alertas.CountAsync());
        Assert.Equal(mensagensAntes, c.Fila.Mensagens.Count);
    }

    [Fact]
    public async Task ComPacoteIncompleto_SemNr18_AbreFicha()
    {
        var c = await MontarAsync();
        var joao = await AdmitidoEDepoisTrocadoAsync(c);
        await DarTreinamentosAsync(c, joao, nr18: false);

        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.TrocaFuncao);

        var planos = await c.Db.PlanosIntegracao.ToListAsync();
        Assert.Equal(StatusPlanoIntegracao.Aberto, planos.Single(p => p.Motivo == MotivoPlanoIntegracao.TrocaFuncao).Status);
        Assert.Equal(StatusPlanoIntegracao.Cancelado, planos.Single(p => p.Motivo == MotivoPlanoIntegracao.Admissao).Status);
    }

    [Fact]
    public async Task PacoteComTreinamentoForaDaValidade_AbreFicha()
    {
        var c = await MontarAsync();
        var joao = await AdmitidoEDepoisTrocadoAsync(c);
        await DarTreinamentosAsync(c, joao, validadeEmDias: -5);

        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.TransferenciaObra);

        Assert.Contains(await c.Db.PlanosIntegracao.ToListAsync(), p => p.Motivo == MotivoPlanoIntegracao.TransferenciaObra);
    }

    [Fact]
    public async Task Admissao_SempreAbreFicha_MesmoComPacoteValido()
    {
        var c = await MontarAsync();
        var joao = await c.TrabalhadorAsync(c.Armador);
        await DarTreinamentosAsync(c, joao);

        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.Admissao);

        Assert.Equal(MotivoPlanoIntegracao.Admissao, (await c.Db.PlanosIntegracao.SingleAsync()).Motivo);
    }

    [Fact]
    public async Task ChaveDesligada_PacoteValido_VoltaAoComportamentoAntigo_AbreFicha()
    {
        var c = await MontarAsync();
        var joao = await AdmitidoEDepoisTrocadoAsync(c);
        await DarTreinamentosAsync(c, joao);

        await c.ServicoCom(new OpcoesIntegracao { DispensarFichaComPacoteValido = false })
            .SincronizarAsync(joao.Id, MotivoPlanoIntegracao.TrocaFuncao);

        var planos = await c.Db.PlanosIntegracao.ToListAsync();
        Assert.Equal(StatusPlanoIntegracao.Aberto, planos.Single(p => p.Motivo == MotivoPlanoIntegracao.TrocaFuncao).Status);
    }

    [Fact]
    public async Task ChaveLigadaExplicitamente_PacoteValido_Dispensa()
    {
        var c = await MontarAsync();
        var joao = await AdmitidoEDepoisTrocadoAsync(c);
        await DarTreinamentosAsync(c, joao);

        await c.ServicoCom(new OpcoesIntegracao { DispensarFichaComPacoteValido = true })
            .SincronizarAsync(joao.Id, MotivoPlanoIntegracao.TrocaFuncao);

        Assert.Single(await c.Db.PlanosIntegracao.ToListAsync());
    }
}
