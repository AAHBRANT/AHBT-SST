using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Application.Integracoes;
using AAHBRANT.SST.Application.IntegracoesAtendimento;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Queries;
using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Application.PlanosIntegracao;
using AAHBRANT.SST.Application.PlanosIntegracao.Queries;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Application.Tests.PlanosIntegracao;
using AAHBRANT.SST.Application.Tests.Terceirizados;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Tests.IntegracoesAtendimento;

// Etapa da OS, conclusão (Q2), registro "Integração realizada", EPI, plano de entrada e ficha com o atendimento ligado.
public class IntegracaoAtendimentoOsETransversaisTests
{
    // Roteia a minuta de cada pessoa: pendências configuradas por trabalhador (sem entrada = emitível).
    private static void ConfigurarMinutas(CenarioIntegracao c, Dictionary<Guid, string[]> pendenciasPorPessoa) =>
        c.RespostasExtras = (req, _) => req switch
        {
            ObterMinutaOrdemServicoQuery q => Task.FromResult<object?>(
                CenarioIntegracao.Minuta(q.TrabalhadorId, pendenciasPorPessoa.GetValueOrDefault(q.TrabalhadorId, []))),
            _ => throw new NotSupportedException(req.GetType().Name),
        };

    [Fact]
    public async Task EtapaOs_PendenciasIguaisSaoAgrupadas_ESoMostramSemAsoApto()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var d = await c.NovaPessoaAsync(c.Ajudante, "Davi");
        var id = await c.IniciarDiretoNaOsAsync(c.A, c.B, d);
        ConfigurarMinutas(c, new()
        {
            [c.A.Id] = ["SemAso"], [c.B.Id] = ["AsoInvalido"], [d.Id] = ["SemAso", "TreinamentoPendente"],
        });

        var os = await new ListarOsIntegracaoQueryHandler(c.Db, c.Mediator).Handle(new ListarOsIntegracaoQuery(id), default);

        Assert.True(os.EtapaOsEmAndamento);
        Assert.Equal(3, os.TotalPessoas);
        var semAso = Assert.Single(os.PendenciasAgrupadas, p => p.Tipo == "SemAsoApto");
        Assert.Equal("Sem ASO apto", semAso.Titulo);
        Assert.Equal(3, semAso.TrabalhadoresIds.Count);
        Assert.Equal(3, semAso.Nomes.Count);
        Assert.Equal(2, os.PendenciasAgrupadas.Count); // "Sem ASO apto" (3 pessoas) + o treinamento pendente de uma
        Assert.All(os.Pessoas, p => Assert.Equal(SituacaoOsIntegracao.Pendente, p.Situacao));
        Assert.False(os.Pessoas.First().PodeEmitir);
    }

    [Fact]
    public async Task Concluir_Q2_OsPendenteNaoSeguraOsDemais_MasOsEmitivelNaoAssinadaSegura()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var pendente = await c.NovaPessoaAsync(c.Ajudante, "Carla Sem ASO");
        var id = await c.IniciarDiretoNaOsAsync(c.A, c.B, pendente);
        ConfigurarMinutas(c, new() { [pendente.Id] = ["SemAso"] });
        await c.OsAssinadaAsync(c.A.Id);

        // B é emitível e ainda não assinou: segura.
        var ex = await Assert.ThrowsAsync<IntegracaoBloqueadaException>(() => c.Concluir().Handle(new ConcluirIntegracaoCommand(id), default));
        Assert.Single(ex.Faltas);
        Assert.Contains("Beto", ex.Faltas[0]);
        Assert.Equal(StatusIntegracaoAtendimento.EmAndamento, (await c.Db.IntegracoesAtendimento.SingleAsync()).Status);

        // B assina (funcionário e técnico); a pendente continua pendente e não segura.
        var osB = await c.OsAssinadaAsync(c.B.Id);
        var dto = await c.Concluir().Handle(new ConcluirIntegracaoCommand(id), default);

        Assert.Equal("concluido", dto.Status);
        Assert.Null(dto.EtapaAtualOrdem);
        Assert.Equal("concluida", dto.Etapas[4].Status);
        var registros = await c.Db.RegistrosIntegracao.ToListAsync();
        Assert.Equal(new[] { c.A.Id, c.B.Id }.OrderBy(i => i), registros.Select(r => r.TrabalhadorId).OrderBy(i => i));
        Assert.All(registros, r => Assert.Equal(id, r.IntegracaoAtendimentoId));
        Assert.Equal(osB.Id, (await c.Db.ParticipantesIntegracao.SingleAsync(p => p.TrabalhadorId == c.B.Id)).OrdemServicoId);

        // Concluir de novo não duplica nada.
        await c.Concluir().Handle(new ConcluirIntegracaoCommand(id), default);
        Assert.Equal(2, await c.Db.RegistrosIntegracao.CountAsync());

        // A pendente assina a OS depois, pelo perfil: o gancho completa o registro dela.
        var osPendente = await c.OsAssinadaAsync(pendente.Id);
        await RegistroIntegracaoSincronizador.SincronizarPorOrdemServicoAsync(c.Db, osPendente.Id, default);
        Assert.Equal(3, await c.Db.RegistrosIntegracao.CountAsync());
        Assert.Contains(await c.Db.RegistrosIntegracao.ToListAsync(), r => r.TrabalhadorId == pendente.Id);
    }

    [Fact]
    public async Task Concluir_SoNaEtapaDaOs_EOsAnteriorAIntegracaoNaoVale()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        var naEtapaDeCurso = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Concluir().Handle(new ConcluirIntegracaoCommand(id), default));
        Assert.Contains("última etapa", naEtapaDeCurso.Message);

        var c2 = await CenarioIntegracao.CriarAsync();
        var id2 = await c2.IniciarDiretoNaOsAsync(c2.A);
        ConfigurarMinutas(c2, new());
        // OS válida assinada ANTES do início da integração: cada pessoa assina a própria OS agora.
        var antiga = await c2.OsAssinadaAsync(c2.A.Id);
        var docs = await c2.Db.DocumentoSignatarios.ToListAsync();
        foreach (var s in docs) s.AssinadoEm = DateTime.UtcNow.AddDays(-30);
        await c2.Db.SaveChangesAsync();

        var os = await new ListarOsIntegracaoQueryHandler(c2.Db, c2.Mediator).Handle(new ListarOsIntegracaoQuery(id2), default);

        Assert.Equal(SituacaoOsIntegracao.NaoEmitida, os.Pessoas.Single().Situacao);
        Assert.False(os.PodeConcluir);
        Assert.NotEqual(Guid.Empty, antiga.Id);
    }

    [Fact]
    public async Task OsDaIntegracao_NaoExigeOCursoIntegracao_ComOAtendimentoLigado()
    {
        var c = await CenarioIntegracao.CriarAsync();
        c.Db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao { FuncaoId = c.Ajudante.Id, CursoTreinamentoId = c.CursoIntegracao.Id });
        await c.Db.SaveChangesAsync();

        var ligado = await new ObterMinutaOrdemServicoQueryHandler(c.Db, Options.Create(new OpcoesIntegracao { AtendimentoHabilitado = true }))
            .Handle(new ObterMinutaOrdemServicoQuery(c.B.Id), default);
        var desligado = await new ObterMinutaOrdemServicoQueryHandler(c.Db).Handle(new ObterMinutaOrdemServicoQuery(c.B.Id), default);

        Assert.NotNull(ligado);
        Assert.DoesNotContain(ligado!.Treinamentos, t => t.Curso == "Integração de Segurança");
        Assert.DoesNotContain(ligado.Pendencias, p => p.Tipo == "TreinamentoPendente");
        // Desligado, tudo como antes: o curso continua exigido.
        Assert.Contains(desligado!.Treinamentos, t => t.Curso == "Integração de Segurança" && t.Situacao == "NaoRealizado");
        Assert.Contains(desligado.Pendencias, p => p.Tipo == "TreinamentoPendente");
    }

    // ---------- Entrega de EPI (Q7) ----------

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

    // ---------- Plano de entrada e ficha com o atendimento ligado ----------

    [Fact]
    public async Task PlanoDeEntrada_MostraIntegracaoEmAndamentoComAEtapa_EFechaOPassoComORegistro()
    {
        var c = await CenarioIntegracao.CriarAsync();
        c.Db.PlanosIntegracao.Add(new PlanoIntegracao { TrabalhadorId = c.A.Id, ObraId = c.Obra.Id, FuncaoId = c.Operador.Id });
        await c.Db.SaveChangesAsync();
        var consulta = new ListarPlanosIntegracaoPendentesQueryHandler(c.Db, Options.Create(c.Opcoes));

        var antes = (await consulta.Handle(new ListarPlanosIntegracaoPendentesQuery(), default)).Single();
        var passoAntes = antes.Passos.Single(p => p.Codigo == "integracao");
        Assert.Equal(SituacaoPasso.Pendente, passoAntes.Situacao);
        Assert.DoesNotContain(antes.Passos, p => p.Situacao == SituacaoPasso.Agendado);

        var id = await c.IniciarAsync(c.A);
        var durante = (await consulta.Handle(new ListarPlanosIntegracaoPendentesQuery(), default)).Single();
        var passo = durante.Passos.Single(p => p.Codigo == "integracao");
        Assert.Equal(SituacaoPasso.EmAndamento, passo.Situacao);
        Assert.Contains("etapa 1 de 5", passo.Detalhe);
        Assert.Equal(id, passo.IntegracaoAtendimentoId);
        Assert.Equal(SituacaoPasso.EmAndamento, durante.Passos.Single(p => p.Codigo == "nr06").Situacao);
        // NR-12/NR-18/NR-35 são etapas da integração, não "demais cursos da função".
        Assert.Equal(SituacaoPasso.Feito, durante.Passos.Single(p => p.Codigo == "cursos").Situacao);

        c.Db.RegistrosIntegracao.Add(new RegistroIntegracao
        {
            ObraId = c.Obra.Id, TrabalhadorId = c.A.Id, IntegracaoAtendimentoId = id, RealizadaEm = DateTime.UtcNow,
            TecnicoNome = "Técnico", FuncaoNome = "Operador",
        });
        await c.Db.SaveChangesAsync();
        var depois = (await consulta.Handle(new ListarPlanosIntegracaoPendentesQuery(), default)).Single();
        Assert.Equal(SituacaoPasso.Feito, depois.Passos.Single(p => p.Codigo == "integracao").Situacao);
    }

    private static PlanoIntegracaoService Servico(CenarioIntegracao c, OpcoesIntegracao opcoes) =>
        new(c.Db, new TecnicosSegurancaPorObraServiceFalso { UsuarioIdsARetornar = { Guid.NewGuid() } },
            new FilaNotificacaoTeamsFalsa(), new FilaCalendarioTeamsFalsa(), new GeradorNumeroSequencialFalso(), opcoes: Options.Create(opcoes));

    [Fact]
    public async Task FichaDeEntrada_ComAChaveDesligada_ContinuaAgendandoOCombo()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var novo = await c.NovaPessoaAsync(c.Operador, "Novo Operador");

        await Servico(c, new OpcoesIntegracao { AtendimentoHabilitado = false }).SincronizarAsync(novo.Id, MotivoPlanoIntegracao.Admissao);

        var cursosAgendados = (await c.Db.ParticipantesSessaoTreinamento.Where(p => p.TrabalhadorId == novo.Id)
            .Select(p => p.SessaoTreinamento!.CursoTreinamentoId).ToListAsync());
        Assert.Contains(c.Nr06.Id, cursosAgendados);
        Assert.Contains(c.CursoIntegracao.Id, cursosAgendados);
    }

    [Fact]
    public async Task TrocaDeFuncao_ComOAtendimentoLigado_DispensaFichaQuemNaoPrecisaDeNenhumaEtapaDeCurso()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var pessoa = await c.NovaPessoaAsync(c.Operador, "Operador Experiente");
        await c.DarTreinamentoValidoAsync(pessoa, c.Nr06, c.Nr12, c.Nr18, c.Nr35);

        await Servico(c, new OpcoesIntegracao { AtendimentoHabilitado = true, DispensarFichaComPacoteValido = true })
            .SincronizarAsync(pessoa.Id, MotivoPlanoIntegracao.TrocaFuncao);

        Assert.Equal(StatusPlanoIntegracao.Dispensado, (await c.Db.PlanosIntegracao.SingleAsync(p => p.TrabalhadorId == pessoa.Id)).Status);
    }
}
