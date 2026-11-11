using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Application.Integracoes;
using AAHBRANT.SST.Application.IntegracoesAtendimento;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Queries;
using AAHBRANT.SST.Application.PlanosIntegracao;
using AAHBRANT.SST.Application.PlanosIntegracao.Queries;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Application.Tests.PlanosIntegracao;
using AAHBRANT.SST.Application.Tests.Terceirizados;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Tests.IntegracoesAtendimento;

// Decisões Q4, Q6, Q9 e Q10 do usuário e correções da revisão (A2, A3, M1-M5, B1, B2, B5).
public class IntegracaoAtendimentoRevisaoTests
{
    private static PlanoIntegracaoService Servico(CenarioIntegracao c, OpcoesIntegracao opcoes) =>
        new(c.Db, new TecnicosSegurancaPorObraServiceFalso { UsuarioIdsARetornar = { Guid.NewGuid() } },
            new FilaNotificacaoTeamsFalsa(), new FilaCalendarioTeamsFalsa(), new GeradorNumeroSequencialFalso(), opcoes: Options.Create(opcoes));

    private static async Task<CursoTreinamento> ExigirNr10Async(CenarioIntegracao c)
    {
        var nr10 = new CursoTreinamento { Nome = "NR-10 Eletricidade", NormaReferencia = "NR-10", CargaHorariaMinima = 40, ValidadeEmMeses = 24 };
        c.Db.CursosTreinamento.Add(nr10);
        c.Db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao { FuncaoId = c.Operador.Id, CursoTreinamentoId = nr10.Id });
        await c.Db.SaveChangesAsync();
        return nr10;
    }

    private static void Minutas(CenarioIntegracao c, Dictionary<Guid, string[]> porPessoa) =>
        c.RespostasExtras = (req, _) => req switch
        {
            AAHBRANT.SST.Application.OrdensServico.ObterMinutaOrdemServicoQuery q => Task.FromResult<object?>(
                CenarioIntegracao.Minuta(q.TrabalhadorId, porPessoa.GetValueOrDefault(q.TrabalhadorId, []))),
            _ => throw new NotSupportedException(req.GetType().Name),
        };

    // ---------- Q4: nenhum curso é agendado automaticamente ----------

    [Fact]
    public async Task Q4_FichaDeEntrada_ComAtendimentoLigado_NaoAgendaCursoNenhum()
    {
        var c = await CenarioIntegracao.CriarAsync();
        await ExigirNr10Async(c);
        var novo = await c.NovaPessoaAsync(c.Operador, "Novo Operador");

        await Servico(c, new OpcoesIntegracao { AtendimentoHabilitado = true }).SincronizarAsync(novo.Id, MotivoPlanoIntegracao.Admissao);

        Assert.Empty(await c.Db.ParticipantesSessaoTreinamento.Where(p => p.TrabalhadorId == novo.Id).ToListAsync());
        Assert.Empty(await c.Db.SessoesTreinamento.ToListAsync());
        Assert.Equal(StatusPlanoIntegracao.Aberto, (await c.Db.PlanosIntegracao.SingleAsync(p => p.TrabalhadorId == novo.Id)).Status);
    }

    [Fact]
    public async Task Q4_FichaDeEntrada_ComAChaveDesligada_AindaAgendaOsDemaisCursos()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var nr10 = await ExigirNr10Async(c);
        var novo = await c.NovaPessoaAsync(c.Operador, "Novo Operador");

        await Servico(c, new OpcoesIntegracao { AtendimentoHabilitado = false }).SincronizarAsync(novo.Id, MotivoPlanoIntegracao.Admissao);

        var cursos = await c.Db.ParticipantesSessaoTreinamento.Where(p => p.TrabalhadorId == novo.Id)
            .Select(p => p.SessaoTreinamento!.CursoTreinamentoId).ToListAsync();
        Assert.Contains(nr10.Id, cursos);
    }

    [Fact]
    public async Task Q4_DispensaDeFicha_ComAtendimentoLigado_NaoAgendaNr10()
    {
        var c = await CenarioIntegracao.CriarAsync();
        await ExigirNr10Async(c);
        var pessoa = await c.NovaPessoaAsync(c.Operador, "Operador Experiente");
        await c.DarTreinamentoValidoAsync(pessoa, c.Nr06, c.Nr12, c.Nr18, c.Nr35);

        await Servico(c, new OpcoesIntegracao { AtendimentoHabilitado = true, DispensarFichaComPacoteValido = true })
            .SincronizarAsync(pessoa.Id, MotivoPlanoIntegracao.TrocaFuncao);

        Assert.Equal(StatusPlanoIntegracao.Dispensado, (await c.Db.PlanosIntegracao.SingleAsync(p => p.TrabalhadorId == pessoa.Id)).Status);
        Assert.Empty(await c.Db.SessoesTreinamento.ToListAsync());
    }

    // ---------- Q6: data do certificado = dia da assinatura de cada pessoa ----------

    [Fact]
    public async Task Q6_CertificadoTemADataDaAssinaturaDeCadaPessoa()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        await c.CompletarEtapaAtualAsync(id, 1);
        var turma = (await c.EtapaAsync(id, 1)).SessaoTreinamentoId!.Value;
        foreach (var p in await c.Db.ParticipantesSessaoTreinamento.Where(p => p.SessaoTreinamentoId == turma).ToListAsync())
            p.PresencaConfirmadaEm = p.TrabalhadorId == c.A.Id ? new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc) : new DateTime(2026, 11, 4, 15, 0, 0, DateTimeKind.Utc);
        await c.Db.SaveChangesAsync();

        await c.AvancarAsync(id);

        var certA = await c.Db.Treinamentos.SingleAsync(t => t.TrabalhadorId == c.A.Id);
        var certB = await c.Db.Treinamentos.SingleAsync(t => t.TrabalhadorId == c.B.Id);
        Assert.Equal(new DateTime(2026, 11, 3), certA.DataRealizacao.Date);
        Assert.Equal(new DateTime(2026, 11, 4), certB.DataRealizacao.Date);
        Assert.Equal(new DateTime(2027, 11, 3), certA.DataValidade.Date);
    }

    // ---------- M2: carga real, ao menos um período ----------

    [Fact]
    public async Task M2_Avancar_ExigeAoMenosUmPeriodo_MesmoComMinimoZero()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        await c.CompletarEtapaAtualAsync(id, 1, comPeriodo: false); // NR-06: mínimo 0

        var ex = await Assert.ThrowsAsync<IntegracaoBloqueadaException>(() => c.AvancarAsync(id));

        Assert.Single(ex.Faltas);
        Assert.Contains("Carga horária", ex.Faltas[0]);
    }

    [Fact]
    public async Task M2_Certificado_GravaACargaLancada_NuncaAPadraoDoCurso()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        await c.CompletarEtapaAtualAsync(id, 1, comPeriodo: false);
        var etapa = await c.EtapaAsync(id, 1);
        await new RegistrarPeriodoEtapaIntegracaoCommandHandler(c.Db).Handle(new RegistrarPeriodoEtapaIntegracaoCommand(
            id, etapa.Id, CenarioIntegracao.DataDaEtapa(1), "08:00", "10:30"), default); // 2 h 30 min

        var dto = await c.AvancarAsync(id);

        Assert.Equal(150, dto.Etapas[0].CargaLancadaMinutos);
        var cert = await c.Db.Treinamentos.SingleAsync(t => t.TrabalhadorId == c.A.Id);
        Assert.Equal(2, cert.CargaHorariaRealizada); // horas inteiras, arredondadas para baixo (padrão do curso seria 1)
    }


    // ---------- A2: períodos ----------

    [Fact]
    public async Task A2_Periodo_RecusaDataAntesDaEtapa_FimNoFuturo_ESobreposicaoNaMesmaEtapa()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        var etapa = await c.EtapaAsync(id, 1);
        var dia = await c.RecuarEtapaAsync(etapa.Id);
        var h = new RegistrarPeriodoEtapaIntegracaoCommandHandler(c.Db);
        var hoje = ExigenciasEtapasIntegracao.HojeBrasilia();

        await h.Handle(new RegistrarPeriodoEtapaIntegracaoCommand(id, etapa.Id, dia, "08:00", "10:00"), default);

        var antes = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Handle(new RegistrarPeriodoEtapaIntegracaoCommand(id, etapa.Id, hoje.AddDays(-11).ToString("yyyy-MM-dd"), "08:00", "10:00"), default));
        Assert.Contains("anterior ao início", antes.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Handle(new RegistrarPeriodoEtapaIntegracaoCommand(id, etapa.Id, hoje.ToString("yyyy-MM-dd"), "23:58", "23:59"), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Handle(new RegistrarPeriodoEtapaIntegracaoCommand(id, etapa.Id, dia, "09:00", "11:00"), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Handle(new RegistrarPeriodoEtapaIntegracaoCommand(id, etapa.Id, dia, "12:00", "11:00"), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Handle(new RegistrarPeriodoEtapaIntegracaoCommand(id, etapa.Id, "2099-01-01", "08:00", "10:00"), default));
        Assert.Equal(120, (await c.ObterAsync(id)).Etapas[0].CargaLancadaMinutos);
    }

    [Fact]
    public async Task A2_MesmaHoraNaoContaParaDoisCursos()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        await c.CompletarEtapaAtualAsync(id, 1);
        await c.AvancarAsync(id);
        var etapa2 = await c.EtapaAsync(id, 2);
        await c.RecuarEtapaAsync(etapa2.Id);
        var h = new RegistrarPeriodoEtapaIntegracaoCommandHandler(c.Db);
        var diaDaEtapa1 = CenarioIntegracao.DataDaEtapa(1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Handle(new RegistrarPeriodoEtapaIntegracaoCommand(id, etapa2.Id, diaDaEtapa1, "08:30", "09:30"), default));
        Assert.Contains("outra etapa", ex.Message);

        await h.Handle(new RegistrarPeriodoEtapaIntegracaoCommand(id, etapa2.Id, diaDaEtapa1, "09:00", "10:00"), default);
    }

    // ---------- M1: conclusão ----------

    [Fact]
    public async Task M1_PendenciaDeSstSegura_ComMensagemDeQuemResolve_EPendenciaDoRhNao()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarDiretoNaOsAsync(c.A, c.B);
        Minutas(c, new() { [c.A.Id] = ["AtividadesFuncaoNaoConfirmadas"] });
        await c.OsAssinadaAsync(c.B.Id);

        var os = await new ListarOsIntegracaoQueryHandler(c.Db, c.Mediator).Handle(new ListarOsIntegracaoQuery(id), default);
        var linhaA = os.Pessoas.Single(p => p.TrabalhadorId == c.A.Id);
        Assert.Equal(SituacaoOsIntegracao.Bloqueada, linhaA.Situacao);
        Assert.Contains("resolve: SST", linhaA.Pendencias[0]);
        Assert.False(os.PodeConcluir);

        var ex = await Assert.ThrowsAsync<IntegracaoBloqueadaException>(() => c.Concluir().Handle(new ConcluirIntegracaoCommand(id), default));
        Assert.Contains("segura a conclusão", ex.Faltas[0]);

        Minutas(c, new() { [c.A.Id] = ["SemAso"] });
        var dto = await c.Concluir().Handle(new ConcluirIntegracaoCommand(id), default);
        Assert.Equal("concluido", dto.Status);
    }

    [Fact]
    public async Task M1_ExigeAoMenosUmaOsAssinada()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarDiretoNaOsAsync(c.A, c.B);
        Minutas(c, new() { [c.A.Id] = ["SemAso"], [c.B.Id] = ["SemAso"] });

        var ex = await Assert.ThrowsAsync<IntegracaoBloqueadaException>(() => c.Concluir().Handle(new ConcluirIntegracaoCommand(id), default));

        Assert.Contains("Nenhuma Ordem de Serviço assinada", ex.Faltas[0]);
        var linha = new LinhaOsIntegracaoDto(Guid.NewGuid(), "x", "y", SituacaoOsIntegracao.Pendente, null, null, null, false, []);
        Assert.False(AvaliadorConclusaoIntegracao.PodeConcluir([linha]));
    }

    // ---------- Q9: "Não assinou" ----------

    [Fact]
    public async Task Q9_NaoAssinou_ExigeMotivoDe5Caracteres_ESoNaEtapaEmAndamento()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        var etapa1 = await c.EtapaAsync(id, 1);
        var etapa3 = await c.EtapaAsync(id, 3);
        var h = new MarcarNaoAssinouEtapaIntegracaoCommandHandler(c.Db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle(new MarcarNaoAssinouEtapaIntegracaoCommand(id, etapa1.Id, c.B.Id, "falt"), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle(new MarcarNaoAssinouEtapaIntegracaoCommand(id, etapa3.Id, c.B.Id, "Faltou ao treinamento"), default));
        Assert.Empty(await c.Db.NaoAssinouEtapasIntegracao.ToListAsync());
    }

    [Fact]
    public async Task Q9_QuemNaoAssinou_SaiDaTurma_NaoTravaOAvanco_NaoRecebeCertificado()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        var etapa1 = await c.EtapaAsync(id, 1);
        var dto = await new MarcarNaoAssinouEtapaIntegracaoCommandHandler(c.Db, new AuditoriaIntegracaoFalsa())
            .Handle(new MarcarNaoAssinouEtapaIntegracaoCommand(id, etapa1.Id, c.B.Id, "Faltou ao treinamento"), default);

        var b = dto.Etapas[0].Participantes.Single(p => p.TrabalhadorId == c.B.Id);
        Assert.True(b.NaoAssinou);
        Assert.Equal("Faltou ao treinamento", b.MotivoNaoAssinou);
        Assert.Equal(1, await c.Db.ParticipantesSessaoTreinamento.CountAsync(p => p.SessaoTreinamentoId == etapa1.SessaoTreinamentoId && p.Ativo));

        await c.CompletarEtapaAtualAsync(id, 1);
        var depois = await c.AvancarAsync(id);

        Assert.Equal("concluida", depois.Etapas[0].Status);
        Assert.Equal(c.A.Id, (await c.Db.Treinamentos.SingleAsync()).TrabalhadorId);
    }

    [Fact]
    public async Task Q9_Desfazer_VoltaAPessoaParaALista_EQuemJaAssinouNaoPodeSerMarcado()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        var etapa1 = await c.EtapaAsync(id, 1);
        var marcar = new MarcarNaoAssinouEtapaIntegracaoCommandHandler(c.Db);
        await marcar.Handle(new MarcarNaoAssinouEtapaIntegracaoCommand(id, etapa1.Id, c.B.Id, "Faltou ao treinamento"), default);

        var dto = await new DesfazerNaoAssinouEtapaIntegracaoCommandHandler(c.Db)
            .Handle(new DesfazerNaoAssinouEtapaIntegracaoCommand(id, etapa1.Id, c.B.Id), default);

        Assert.False(dto.Etapas[0].Participantes.Single(p => p.TrabalhadorId == c.B.Id).NaoAssinou);
        Assert.Equal(2, await c.Db.ParticipantesSessaoTreinamento.CountAsync(p => p.SessaoTreinamentoId == etapa1.SessaoTreinamentoId && p.Ativo));

        var b = await c.Db.ParticipantesSessaoTreinamento.FirstAsync(p => p.SessaoTreinamentoId == etapa1.SessaoTreinamentoId && p.TrabalhadorId == c.B.Id && p.Ativo);
        b.PresencaConfirmadaEm = DateTime.UtcNow;
        await c.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            marcar.Handle(new MarcarNaoAssinouEtapaIntegracaoCommand(id, etapa1.Id, c.B.Id, "Faltou ao treinamento"), default));
    }

    [Fact]
    public async Task Q9_TodosMarcados_AvancaSemCertificadoNemFotos()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        var etapa1 = await c.EtapaAsync(id, 1);
        var marcar = new MarcarNaoAssinouEtapaIntegracaoCommandHandler(c.Db);
        await marcar.Handle(new MarcarNaoAssinouEtapaIntegracaoCommand(id, etapa1.Id, c.A.Id, "Faltou ao treinamento"), default);
        await marcar.Handle(new MarcarNaoAssinouEtapaIntegracaoCommand(id, etapa1.Id, c.B.Id, "Faltou ao treinamento"), default);

        var dto = await c.AvancarAsync(id);

        Assert.Equal("concluida", dto.Etapas[0].Status);
        Assert.Equal(2, dto.EtapaAtualOrdem);
        Assert.Empty(await c.Db.Treinamentos.ToListAsync());
        Assert.Equal(StatusSessaoTreinamento.Concluida, (await c.Db.SessoesTreinamento.SingleAsync(s => s.Id == etapa1.SessaoTreinamentoId)).Status);
    }

    [Fact]
    public async Task Q9_RegistroIntegracaoNaoSaiEnquantoFaltaCursoExigido_ESaiQuandoAPessoaTemTodos()
    {
        var c = await CenarioIntegracao.CriarAsync();
        await c.DarTreinamentoValidoAsync(c.A, c.Nr06, c.Nr12, c.Nr18, c.Nr35);
        await c.DarTreinamentoValidoAsync(c.B, c.Nr06);
        var id = await c.IniciarAsync(c.A, c.B);
        var etapa = (await c.ObterAsync(id)).Etapas.Single(e => e.Status == "emAndamento");
        Assert.Equal("nr18", etapa.Tipo);
        await new MarcarNaoAssinouEtapaIntegracaoCommandHandler(c.Db)
            .Handle(new MarcarNaoAssinouEtapaIntegracaoCommand(id, etapa.Id, c.B.Id, "Faltou ao treinamento"), default);
        await c.AvancarAsync(id);
        Minutas(c, new());
        await c.OsAssinadaAsync(c.A.Id);
        await c.OsAssinadaAsync(c.B.Id);

        await c.Concluir().Handle(new ConcluirIntegracaoCommand(id), default);

        Assert.Equal(new[] { c.A.Id }, (await c.Db.RegistrosIntegracao.ToListAsync()).Select(r => r.TrabalhadorId));

        await c.DarTreinamentoValidoAsync(c.B, c.Nr18);
        await RegistroIntegracaoSincronizador.SincronizarPorTrabalhadorAsync(c.Db, c.B.Id, c.Obra.Id, default);
        Assert.Equal(2, await c.Db.RegistrosIntegracao.CountAsync());
    }

    // ---------- B2: desligado na abertura da etapa ----------

    [Fact]
    public async Task B2_Desligado_NaoEInscrito_EntraComoNaoAssinouComMotivoDesligado()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        await c.CompletarEtapaAtualAsync(id, 1);
        await c.AvancarAsync(id);
        var b = await c.Db.Trabalhadores.SingleAsync(t => t.Id == c.B.Id);
        b.Situacao = SituacaoTrabalhador.Desligado;
        await c.Db.SaveChangesAsync();
        await c.CompletarEtapaAtualAsync(id, 2);

        var dto = await c.AvancarAsync(id);

        var etapa = dto.Etapas[2];
        Assert.Equal("emAndamento", etapa.Status);
        var pessoaB = etapa.Participantes.Single(p => p.TrabalhadorId == c.B.Id);
        Assert.True(pessoaB.NaoAssinou);
        Assert.Equal("Desligado", pessoaB.MotivoNaoAssinou);
        Assert.Equal(1, await c.Db.ParticipantesSessaoTreinamento.CountAsync(p => p.SessaoTreinamentoId == etapa.SessaoTreinamentoId && p.Ativo));
    }

    // ---------- Q10: listagem ----------

    [Fact]
    public async Task Q10_Listagem_FiltraPorStatus_TrazEtapaAtual_ERespeitaOEscopo()
    {
        var c = await CenarioIntegracao.CriarAsync();
        await c.IniciarAsync(c.A, c.B);
        var h = new ListarIntegracoesQueryHandler(c.Db);

        var abertas = await h.Handle(new ListarIntegracoesQuery(null, "emAndamento"), default);
        var item = Assert.Single(abertas);
        Assert.Equal(c.Obra.Nome, item.ObraNome);
        Assert.Equal(2, item.Participantes);
        Assert.Equal(1, item.EtapaAtualOrdem);
        Assert.Contains("NR-06", item.EtapaAtualTitulo);
        Assert.Equal("emAndamento", item.Status);
        Assert.Empty(await h.Handle(new ListarIntegracoesQuery(c.Obra.Id, "concluido"), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle(new ListarIntegracoesQuery(null, "xyz"), default));

        var usuario = new CurrentUserService();
        usuario.DefinirEscopo(false, [Guid.NewGuid()]);
        Assert.Empty(await new ListarIntegracoesQueryHandler(new SstDbContext(c.OpcoesDb, usuario)).Handle(new ListarIntegracoesQuery(), default));
    }

    // ---------- M3 e M4: plano de entrada ----------

    [Fact]
    public async Task M3_RegistroDeOutraObraNaoConta_ERegistroNaoFechaOPassoDaNr06()
    {
        var c = await CenarioIntegracao.CriarAsync();
        c.Db.PlanosIntegracao.Add(new PlanoIntegracao { TrabalhadorId = c.A.Id, ObraId = c.Obra.Id, FuncaoId = c.Operador.Id });
        c.Db.RegistrosIntegracao.Add(new RegistroIntegracao
        {
            ObraId = Guid.NewGuid(), TrabalhadorId = c.A.Id, IntegracaoAtendimentoId = Guid.NewGuid(), RealizadaEm = DateTime.UtcNow,
            TecnicoNome = "T", FuncaoNome = "F",
        });
        await c.Db.SaveChangesAsync();
        var consulta = new ListarPlanosIntegracaoPendentesQueryHandler(c.Db, Options.Create(c.Opcoes));

        var outraObra = (await consulta.Handle(new ListarPlanosIntegracaoPendentesQuery(), default)).Single();
        Assert.Equal(SituacaoPasso.Pendente, outraObra.Passos.Single(p => p.Codigo == "integracao").Situacao);

        c.Db.RegistrosIntegracao.Add(new RegistroIntegracao
        {
            ObraId = c.Obra.Id, TrabalhadorId = c.A.Id, IntegracaoAtendimentoId = Guid.NewGuid(), RealizadaEm = DateTime.UtcNow,
            TecnicoNome = "T", FuncaoNome = "F",
        });
        await c.Db.SaveChangesAsync();
        var mesmaObra = (await consulta.Handle(new ListarPlanosIntegracaoPendentesQuery(), default)).Single();
        Assert.Equal(SituacaoPasso.Feito, mesmaObra.Passos.Single(p => p.Codigo == "integracao").Situacao);
        Assert.NotEqual(SituacaoPasso.Feito, mesmaObra.Passos.Single(p => p.Codigo == "nr06").Situacao);
    }

    [Fact]
    public async Task M4_PlanosIds_NaoTrazPlanoJaEncerrado()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var plano = new PlanoIntegracao { TrabalhadorId = c.A.Id, ObraId = c.Obra.Id, FuncaoId = c.Operador.Id, Status = StatusPlanoIntegracao.Concluido };
        c.Db.PlanosIntegracao.Add(plano);
        await c.Db.SaveChangesAsync();

        var lista = await new ListarPlanosIntegracaoPendentesQueryHandler(c.Db, Options.Create(c.Opcoes))
            .Handle(new ListarPlanosIntegracaoPendentesQuery(PlanosIds: [plano.Id]), default);

        Assert.Empty(lista);
    }

    [Fact]
    public async Task Rota_DoPassoDeIntegracao_ApontaParaARotaDoApp()
    {
        var c = await CenarioIntegracao.CriarAsync();
        c.Db.PlanosIntegracao.Add(new PlanoIntegracao { TrabalhadorId = c.A.Id, ObraId = c.Obra.Id, FuncaoId = c.Operador.Id });
        await c.Db.SaveChangesAsync();
        var consulta = new ListarPlanosIntegracaoPendentesQueryHandler(c.Db, Options.Create(c.Opcoes));

        var sem = (await consulta.Handle(new ListarPlanosIntegracaoPendentesQuery(), default)).Single().Passos.Single(p => p.Codigo == "integracao");
        Assert.Equal($"/treinamentos/integracao/nova?obraId={c.Obra.Id}", sem.Rota);

        var id = await c.IniciarAsync(c.A);
        var com = (await consulta.Handle(new ListarPlanosIntegracaoPendentesQuery(), default)).Single().Passos.Single(p => p.Codigo == "integracao");
        Assert.Equal($"/treinamentos/integracao/{id}", com.Rota);
    }

    // ---------- M5: token de captura ----------

    [Fact]
    public async Task M5_FotoDeTurmaDeIntegracao_ExigeTokenDeUsoUnico_EAHoraEDoServidor()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        var sessao = (await c.EtapaAsync(id, 1)).SessaoTreinamentoId!.Value;

        var semToken = await Assert.ThrowsAsync<InvalidOperationException>(() => c.FotoAsync(sessao, 1, CenarioIntegracao.Bytes(1), semToken: true));
        Assert.Contains("token", semToken.Message);

        var token = await c.TokenAsync(sessao);
        var fotoId = await c.FotoAsync(sessao, 1, CenarioIntegracao.Bytes(2), capturadaEm: DateTimeOffset.UtcNow.AddMinutes(-30), token: token);
        var gravada = DadosCapturaFoto.Ler((await c.Db.FotosEvidenciaSessaoTreinamento.SingleAsync(f => f.Id == fotoId)).FotoMetadadosJson)!;
        Assert.True(Math.Abs((gravada.CapturadaEm!.Value - DateTimeOffset.UtcNow).TotalMinutes) < 2);

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.FotoAsync(sessao, 2, CenarioIntegracao.Bytes(3), token: token));

        var velho = await c.TokenAsync(sessao);
        var linha = await c.Db.TokensCapturaFoto.SingleAsync(t => t.Token == velho);
        linha.ExpiraEmUtc = DateTime.UtcNow.AddSeconds(-1);
        await c.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.FotoAsync(sessao, 2, CenarioIntegracao.Bytes(4), token: velho));

        var comum = new SessaoTreinamento { ObraId = c.Obra.Id, CursoTreinamentoId = c.Nr18.Id, DataRealizacao = DateTime.UtcNow.Date, CargaHorariaRealizada = 4 };
        c.Db.SessoesTreinamento.Add(comum);
        await c.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.TokenAsync(comum.Id));
        var outroToken = await c.TokenAsync(sessao);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.FotoAsync(sessao, 3, CenarioIntegracao.Bytes(5), token: outroToken + "x"));
    }

    // ---------- B1: acesso às turmas de integração ----------

    [Fact]
    public async Task B1_TurmaDeIntegracao_SoTecnicoOuAdminDaObra_TurmaComumNaoEAfetada()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        var sessao = (await c.EtapaAsync(id, 1)).SessaoTreinamentoId!.Value;
        var outraObra = new Obra { Codigo = "OUT", Nome = "Outra" };
        var daObra = new Usuario { AzureAdObjectId = "oid-obra", Nome = "T1", Email = "t1@x.com" };
        var deOutra = new Usuario { AzureAdObjectId = "oid-outra", Nome = "T2", Email = "t2@x.com" };
        var perfil = new PerfilAcesso { Tipo = TipoPerfilAcesso.TecnicoSeguranca, Nome = "Técnico" };
        c.Db.Obras.Add(outraObra);
        c.Db.Usuarios.AddRange(daObra, deOutra);
        c.Db.PerfisAcesso.Add(perfil);
        c.Db.UsuariosPerfilObra.AddRange(
            new UsuarioPerfilObra { UsuarioId = daObra.Id, PerfilAcessoId = perfil.Id, ObraId = c.Obra.Id },
            new UsuarioPerfilObra { UsuarioId = deOutra.Id, PerfilAcessoId = perfil.Id, ObraId = outraObra.Id });
        var comum = new SessaoTreinamento { ObraId = c.Obra.Id, CursoTreinamentoId = c.Nr18.Id, DataRealizacao = DateTime.UtcNow.Date, CargaHorariaRealizada = 4 };
        c.Db.SessoesTreinamento.Add(comum);
        await c.Db.SaveChangesAsync();
        var h = new GarantirAcessoTurmaIntegracaoQueryHandler(c.Db);

        Assert.True(await h.Handle(new GarantirAcessoTurmaIntegracaoQuery(sessao, null, "oid-obra", true), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Handle(new GarantirAcessoTurmaIntegracaoQuery(sessao, null, "oid-outra", true), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Handle(new GarantirAcessoTurmaIntegracaoQuery(sessao, null, null, true), default));
        Assert.False(await h.Handle(new GarantirAcessoTurmaIntegracaoQuery(comum.Id, null, "oid-outra", true), default));
    }

    // ---------- A3: entrega de EPI ----------

    private static async Task<(Trabalhador T, CatalogoEpi Epi)> PessoaParaEpiAsync(CenarioIntegracao c)
    {
        var t = await c.NovaPessoaAsync(c.Ajudante, "Pessoa do EPI");
        var epi = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        c.Db.CatalogoEpis.Add(epi);
        c.Db.EstoquesEpi.Add(new EstoqueEpi { CatalogoEpiId = epi.Id, ObraId = c.Obra.Id, Saldo = 10 });
        await c.Db.SaveChangesAsync();
        return (t, epi);
    }

    private static CriarEntregaEpiCommand Entrega(Trabalhador t, CatalogoEpi epi) =>
        new(t.Id, epi.Id, DateTime.UtcNow, null, null, 1, null, null, null, MotivoEntregaEpi.Inicial, null, null);

    [Fact]
    public async Task A3_ComAChaveDesligada_ARegraAntigaContinua()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var h = new CriarEntregaEpiCommandHandler(c.Db);
        var (semNr06, epi) = await PessoaParaEpiAsync(c);
        c.Db.RegistrosIntegracao.Add(new RegistroIntegracao
        {
            ObraId = c.Obra.Id, TrabalhadorId = semNr06.Id, IntegracaoAtendimentoId = Guid.NewGuid(), RealizadaEm = DateTime.UtcNow, TecnicoNome = "T", FuncaoNome = "F",
        });
        await c.DarTreinamentoValidoAsync(semNr06, c.CursoIntegracao);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle(Entrega(semNr06, epi), default));

        var comNr06 = await c.NovaPessoaAsync(c.Ajudante, "Com NR-06");
        await c.DarTreinamentoValidoAsync(comNr06, c.Nr06);
        Assert.NotEqual(Guid.Empty, await h.Handle(Entrega(comNr06, epi), default));
    }

    [Fact]
    public async Task A3_ComAChaveLigada_SoLiberaComNr06ValidaEAssinada_PreenchendoOsDadosDaNr06()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var h = new CriarEntregaEpiCommandHandler(c.Db, null, Options.Create(c.Opcoes));
        var (pessoa, epi) = await PessoaParaEpiAsync(c);
        var assinada = await c.NovaPessoaAsync(c.Ajudante, "Assinou");
        var externa = await c.NovaPessoaAsync(c.Ajudante, "Externa");

        c.Db.RegistrosIntegracao.Add(new RegistroIntegracao
        {
            ObraId = c.Obra.Id, TrabalhadorId = pessoa.Id, IntegracaoAtendimentoId = Guid.NewGuid(), RealizadaEm = DateTime.UtcNow, TecnicoNome = "T", FuncaoNome = "F",
        });
        await c.DarTreinamentoValidoAsync(pessoa, c.CursoIntegracao);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle(Entrega(pessoa, epi), default));

        await c.DarTreinamentoValidoAsync(pessoa, c.Nr06);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle(Entrega(pessoa, epi), default));
        Assert.Contains("assinatura", ex.Message);

        await c.DarTreinamentoValidoAsync(assinada, c.Nr06);
        var cert = await c.Db.Treinamentos.SingleAsync(t => t.TrabalhadorId == assinada.Id);
        var doc = new DocumentoAssinatura { EntidadeTipo = nameof(Treinamento), EntidadeId = cert.Id, ObraId = c.Obra.Id };
        c.Db.DocumentosAssinatura.Add(doc);
        c.Db.DocumentoSignatarios.Add(new DocumentoSignatario
        {
            DocumentoAssinaturaId = doc.Id, TrabalhadorId = assinada.Id, MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria, AssinadoEm = DateTime.UtcNow,
        });
        await c.Db.SaveChangesAsync();
        var entregaId = await h.Handle(Entrega(assinada, epi), default);
        var entrega = await c.Db.EntregasEpi.SingleAsync(e => e.Id == entregaId);
        Assert.Equal(cert.NumeroCertificado, entrega.NumeroListaPresencaNr6);
        Assert.Equal(cert.DataRealizacao, entrega.DataTreinamentoNr6);

        await c.DarTreinamentoValidoAsync(externa, c.Nr06);
        var externo = await c.Db.Treinamentos.SingleAsync(t => t.TrabalhadorId == externa.Id);
        externo.OrigemCertificado = OrigemCertificadoTreinamento.Externo;
        c.Db.Add(new ArquivoCertificadoTreinamento { TreinamentoId = externo.Id, NomeArquivo = "c.pdf", ContentType = "application/pdf", Conteudo = [1, 2, 3], TamanhoBytes = 3 });
        await c.Db.SaveChangesAsync();
        Assert.NotEqual(Guid.Empty, await h.Handle(Entrega(externa, epi), default));
    }
}
