using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Integracoes;
using AAHBRANT.SST.Application.IntegracoesAtendimento;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Queries;
using AAHBRANT.SST.Application.SessoesTreinamento;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Tests.IntegracoesAtendimento;

// 16/11/2026: o instrutor assina com biometria (digital ou facial) uma vez em cada etapa; carga horária automática com espera
// do fim; pré-checagem de biometria do instrutor; pendências estruturadas; turma comum também exige a assinatura antes de encerrar.
public class InstrutorBiometricoETempoAutomaticoTests
{
    private static readonly byte[] Foto = [1, 2, 3, 4];

    // NR-06 com 2 h previstas (a carga do curso vem do catálogo da obra; o cenário base a deixa em 0).
    private static async Task<CenarioIntegracao> NovoCenarioAsync()
    {
        var c = await CenarioIntegracao.CriarAsync();
        c.Nr06.CargaHorariaMinima = 2;
        await c.Db.SaveChangesAsync();
        return c;
    }

    private static DadosAssinaturaInstrutorDigital Digital(Guid trabalhadorId) =>
        new(Guid.NewGuid(), "segredo", trabalhadorId, 90, ImagemDigital: [5, 5, 5],
            Localizacao: new LocalizacaoAssinatura(StatusLocalizacaoAssinatura.Capturada, -7.1, -34.8, 10));

    private static async Task<(Guid IntegracaoId, IntegracaoEtapa Etapa)> EtapaEmAndamentoAsync(CenarioIntegracao c)
    {
        var id = await c.IniciarAsync(c.A, c.B);
        return (id, await c.EtapaAsync(id, 1));
    }

    // ---- Períodos automáticos (função pura) ----

    // Brasília é UTC-3 (sem horário de verão desde 2019).
    private static DateTime Utc(int dia, int hora, int minuto, int segundo = 0) =>
        new DateTime(2026, 11, dia, 0, 0, 0, DateTimeKind.Utc).AddHours(hora + 3).AddMinutes(minuto).AddSeconds(segundo);

    [Fact]
    public void Automatico_MesmoDia_InicioNoMinutoEFimSomaACarga()
    {
        var periodos = PeriodosAutomaticosEtapa.Calcular(Utc(16, 8, 15, 42), 4);

        var p = Assert.Single(periodos);
        Assert.Equal(new DateTime(2026, 11, 16), p.Data);
        Assert.Equal(new TimeSpan(8, 15, 0), p.Inicio);
        Assert.Equal(new TimeSpan(12, 15, 0), p.Fim);
        Assert.Equal(240, p.Minutos);
    }

    [Fact]
    public void Automatico_CruzaAMeiaNoite_DividePorDia_ESomaContinuaIgualACarga()
    {
        var periodos = PeriodosAutomaticosEtapa.Calcular(Utc(16, 22, 0), 8);

        Assert.Equal(2, periodos.Count);
        Assert.Equal(new DateTime(2026, 11, 16), periodos[0].Data);
        Assert.Equal(new TimeSpan(22, 0, 0), periodos[0].Inicio);
        Assert.Equal(new TimeSpan(23, 59, 0), periodos[0].Fim);
        Assert.Equal(120, periodos[0].Minutos);
        Assert.Equal(new DateTime(2026, 11, 17), periodos[1].Data);
        Assert.Equal(TimeSpan.Zero, periodos[1].Inicio);
        Assert.Equal(new TimeSpan(6, 0, 0), periodos[1].Fim);
        Assert.Equal(360, periodos[1].Minutos);
        Assert.Equal(8 * 60, periodos.Sum(x => x.Minutos));
    }

    [Fact]
    public void Automatico_TerminandoExatamenteNaMeiaNoite_FicaEmUmPeriodoSo()
    {
        var periodos = PeriodosAutomaticosEtapa.Calcular(Utc(16, 16, 0), 8);

        var p = Assert.Single(periodos);
        Assert.Equal(new TimeSpan(23, 59, 0), p.Fim);
        Assert.Equal(480, p.Minutos);
    }

    [Fact]
    public void Automatico_SemCargaPrevista_NaoLancaNada() =>
        Assert.Empty(PeriodosAutomaticosEtapa.Calcular(Utc(16, 8, 0), 0));

    // ---- Texto "Carga horária fecha às HH:mm" ----

    private static PeriodoEtapaIntegracao PeriodoAuto(DateTime data, TimeSpan inicio, int minutos) =>
        new() { Data = data, Inicio = inicio, Fim = inicio + TimeSpan.FromMinutes(minutos), Minutos = minutos, Automatico = true };

    [Fact]
    public void AguardandoFim_MostraHoraDeFimEQuantoFalta()
    {
        var periodos = new[] { PeriodoAuto(new DateTime(2026, 11, 16), new TimeSpan(13, 0, 0), 240) };

        var texto = AvaliadorEtapaIntegracao.AguardandoFim(periodos, new DateTime(2026, 11, 16, 13, 55, 0));

        Assert.Equal("Carga horária fecha às 17:00 — faltam 3 h 05 min.", texto);
        Assert.Equal("Carga horária fecha às 17:00 — faltam 40 min.", AvaliadorEtapaIntegracao.AguardandoFim(periodos, new DateTime(2026, 11, 16, 16, 20, 0)));
        Assert.Null(AvaliadorEtapaIntegracao.AguardandoFim(periodos, new DateTime(2026, 11, 16, 17, 0, 0)));
    }

    [Fact]
    public void AguardandoFim_ValeOFimDoUltimoPeriodoAutomatico_EManualNaoEspera()
    {
        var dividido = new[]
        {
            PeriodoAuto(new DateTime(2026, 11, 16), new TimeSpan(22, 0, 0), 120),
            PeriodoAuto(new DateTime(2026, 11, 17), TimeSpan.Zero, 360),
        };
        var agora = new DateTime(2026, 11, 16, 23, 0, 0);

        Assert.Equal("Carga horária fecha às 06:00 de 17/11 — faltam 7 h 00 min.", AvaliadorEtapaIntegracao.AguardandoFim(dividido, agora));

        var manual = new[] { new PeriodoEtapaIntegracao { Data = new DateTime(2026, 11, 16), Inicio = new TimeSpan(22, 0, 0), Fim = new TimeSpan(23, 59, 0), Minutos = 119 } };
        Assert.Null(AvaliadorEtapaIntegracao.AguardandoFim(manual, agora));
    }

    // ---- Pendências do avaliador ----

    [Fact]
    public async Task Avaliador_InstrutorNaoAssinou_GeraPendenciaComCodigo_ESemBiometriaGeraAOutra()
    {
        var c = await NovoCenarioAsync();
        var (id, etapa) = await EtapaEmAndamentoAsync(c);
        var sessao = await c.Db.SessoesTreinamento.AsNoTracking().SingleAsync(s => s.Id == etapa.SessaoTreinamentoId);
        var nomes = new Dictionary<Guid, string>();

        var comBiometria = AvaliadorEtapaIntegracao.Pendencias(etapa, sessao, [], nomes, [], 3, new SituacaoInstrutorEtapa(false, true, false));
        Assert.Contains(comBiometria, p => p.Codigo == "instrutor_assinatura" && p.Texto == "Assinatura biométrica do instrutor pendente.");
        Assert.DoesNotContain(comBiometria, p => p.Codigo == "instrutor_sem_biometria");

        var semBiometria = AvaliadorEtapaIntegracao.Pendencias(etapa, sessao, [], nomes, [], 3, new SituacaoInstrutorEtapa(false, false, false));
        Assert.Contains(semBiometria, p => p.Codigo == "instrutor_sem_biometria"
            && p.Texto == "Instrutor sem digital nem facial cadastrada: cadastre no perfil dele.");

        var assinou = AvaliadorEtapaIntegracao.Pendencias(etapa, sessao, [], nomes, [], 3, new SituacaoInstrutorEtapa(true, true, true));
        Assert.DoesNotContain(assinou, p => p.Codigo.StartsWith("instrutor_"));
    }

    [Fact]
    public async Task Avaliador_CodigosEstaveisParaTodasAsPendencias()
    {
        var c = await NovoCenarioAsync();
        var (_, etapa) = await EtapaEmAndamentoAsync(c);
        var sessao = await c.Db.SessoesTreinamento.AsNoTracking().SingleAsync(s => s.Id == etapa.SessaoTreinamentoId);
        sessao.InstrutorTrabalhadorId = null;
        var participante = new ParticipanteSessaoTreinamento { TrabalhadorId = c.A.Id };
        var nomes = new Dictionary<Guid, string> { [c.A.Id] = "Ana" };

        var codigos = AvaliadorEtapaIntegracao.Pendencias(etapa, sessao, [participante], nomes, [], 0, new SituacaoInstrutorEtapa(false, false, false))
            .Select(p => p.Codigo).ToList();
        Assert.Equal(["instrutor_nao_definido", "carga", "fotos", "assinaturas_colaboradores"], codigos);

        sessao.InstrutorTrabalhadorId = c.Instrutor.Id;
        var semGente = AvaliadorEtapaIntegracao.Pendencias(etapa, sessao, [], nomes,
            [new PeriodoEtapaIntegracao { Minutos = 600 }], 3, new SituacaoInstrutorEtapa(true, true, true)).Select(p => p.Codigo).ToList();
        Assert.Equal(["sem_participantes"], semGente);
    }

    // ---- Abertura da etapa: período automático ----

    [Fact]
    public async Task AbrirEtapa_LancaOPeriodoAutomatico_ETelaVeAFlagEAPendenciaDeEspera()
    {
        var c = await NovoCenarioAsync();
        var (id, etapa) = await EtapaEmAndamentoAsync(c);

        Assert.True(etapa.CargaMinimaHoras > 0, "o curso da etapa tem carga prevista");
        var periodos = await c.Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).ToListAsync();
        Assert.All(periodos, p => Assert.True(p.Automatico));
        Assert.Equal(etapa.CargaMinimaHoras * 60, periodos.Sum(p => p.Minutos));

        var dto = await c.ObterAsync(id);
        Assert.All(dto.Etapas[0].Periodos, p => Assert.True(p.Automatico));
        Assert.Contains(dto.Etapas[0].Pendencias!, p => p.Codigo == "carga_aguardando_fim");
        Assert.Contains(dto.Etapas[0].Faltas, f => f.StartsWith("Carga horária fecha às"));
        // Etapas que ainda não abriram não têm período nem pendência.
        Assert.Empty(dto.Etapas[1].Periodos);
    }

    [Fact]
    public async Task Avancar_EsperaOFimDoPeriodoAutomatico_DepoisLibera()
    {
        var c = await NovoCenarioAsync();
        var (id, etapa) = await EtapaEmAndamentoAsync(c);
        // Tudo pronto (presença, fotos, instrutor), mas o período automático ainda não terminou.
        await ProntoSemAjustarHorarioAsync(c, etapa);

        var ex = await Assert.ThrowsAsync<IntegracaoBloqueadaException>(() => c.AvancarAsync(id));
        var falta = Assert.Single(ex.Faltas);
        Assert.StartsWith("Carga horária fecha às", falta);
        Assert.Contains("faltam", falta);

        // O tempo passa: o período automático termina ontem.
        var automaticos = await c.Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).ToListAsync();
        foreach (var p in automaticos) p.Data = p.Data.AddDays(-2);
        (await c.Db.IntegracaoEtapas.SingleAsync(e => e.Id == etapa.Id)).IniciadaEmUtc = DateTime.UtcNow.AddDays(-2);
        await c.Db.SaveChangesAsync();

        var dto = await c.AvancarAsync(id);
        Assert.Equal("concluida", dto.Etapas[0].Status);
    }

    [Fact]
    public async Task Avancar_AjustarHorario_RemoverOAutomaticoELancarManualTiraAEspera()
    {
        var c = await NovoCenarioAsync();
        var (id, etapa) = await EtapaEmAndamentoAsync(c);
        await ProntoSemAjustarHorarioAsync(c, etapa);

        var dtoAntes = await c.ObterAsync(id);
        foreach (var p in dtoAntes.Etapas[0].Periodos)
            await new RemoverPeriodoEtapaIntegracaoCommandHandler(c.Db).Handle(new RemoverPeriodoEtapaIntegracaoCommand(id, etapa.Id, p.Id), default);
        var dataEtapa = await c.RecuarEtapaAsync(etapa.Id);
        await new RegistrarPeriodoEtapaIntegracaoCommandHandler(c.Db).Handle(new RegistrarPeriodoEtapaIntegracaoCommand(
            id, etapa.Id, dataEtapa, "08:00", TimeSpan.FromHours(8 + Math.Max(1, etapa.CargaMinimaHoras)).ToString(@"hh\:mm")), default);

        var dto = await c.AvancarAsync(id);
        Assert.Equal("concluida", dto.Etapas[0].Status);
    }

    // Presença de todos, 3 fotos e assinatura do instrutor, mantendo o período automático da abertura.
    private static async Task ProntoSemAjustarHorarioAsync(CenarioIntegracao c, IntegracaoEtapa etapa)
    {
        var sessaoId = etapa.SessaoTreinamentoId!.Value;
        foreach (var p in await c.Db.ParticipantesSessaoTreinamento.Where(p => p.SessaoTreinamentoId == sessaoId).ToListAsync())
        {
            p.PresencaConfirmadaEm = DateTime.UtcNow;
            p.MetodoPresenca = MetodoAutenticacaoAssinatura.Biometria;
        }
        await c.Db.SaveChangesAsync();
        for (var ordem = 1; ordem <= 3; ordem++)
            await c.FotoAsync(sessaoId, ordem, CenarioIntegracao.Bytes(700 + ordem));
        await c.AssinarInstrutorAsync(sessaoId);
    }

    // ---- Avançar exige a assinatura biométrica do instrutor ----

    [Fact]
    public async Task Avancar_SemAssinaturaDoInstrutor_Bloqueia_ComAssinatura_Avanca()
    {
        var c = await NovoCenarioAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        await c.CompletarEtapaAtualAsync(id, 1, comInstrutor: false);

        var ex = await Assert.ThrowsAsync<IntegracaoBloqueadaException>(() => c.AvancarAsync(id));
        Assert.Equal(["Assinatura biométrica do instrutor pendente."], ex.Faltas);
        Assert.Empty(await c.Db.Treinamentos.ToListAsync());

        var etapa = await c.EtapaAsync(id, 1);
        await c.AssinarInstrutorAsync(etapa.SessaoTreinamentoId!.Value);
        var dto = await c.AvancarAsync(id);

        Assert.Equal("concluida", dto.Etapas[0].Status);
        Assert.True(dto.Etapas[0].InstrutorAssinou);
        Assert.Equal("biometria", dto.Etapas[0].InstrutorMetodo);
        Assert.NotNull(dto.Etapas[0].InstrutorAssinadoEmUtc);
        Assert.Equal(2, dto.EtapaAtualOrdem);
    }

    [Fact]
    public async Task Avancar_InstrutorSemBiometria_ListaAsDuasPendencias()
    {
        var c = await NovoCenarioAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        await c.CompletarEtapaAtualAsync(id, 1, comInstrutor: false);
        // A biometria foi apagada depois de iniciar (o instrutor não consegue mais assinar).
        c.Db.TemplatesBiometricoFutronic.RemoveRange(await c.Db.TemplatesBiometricoFutronic.ToListAsync());
        await c.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<IntegracaoBloqueadaException>(() => c.AvancarAsync(id));
        Assert.Equal(
            ["Assinatura biométrica do instrutor pendente.", "Instrutor sem digital nem facial cadastrada: cadastre no perfil dele."], ex.Faltas);
        var dto = await c.ObterAsync(id);
        Assert.False(dto.Etapas[0].InstrutorTemDigital);
        Assert.False(dto.Etapas[0].InstrutorTemFacial);
    }

    // ---- Iniciar: pré-checagem do instrutor ----

    [Fact]
    public async Task Iniciar_RecusaInstrutorSemDigitalNemFacial_ComONome()
    {
        var c = await NovoCenarioAsync();
        c.Db.TemplatesBiometricoFutronic.RemoveRange(await c.Db.TemplatesBiometricoFutronic.ToListAsync());
        await c.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.IniciarAsync(c.A));

        Assert.Contains("Ivo Instrutor", ex.Message);
        Assert.Contains("digital nem facial", ex.Message);
        Assert.Empty(await c.Db.IntegracoesAtendimento.ToListAsync());
    }

    [Fact]
    public async Task Iniciar_ComSoFacialCadastrada_Inicia()
    {
        var c = await NovoCenarioAsync();
        c.Db.TemplatesBiometricoFutronic.RemoveRange(await c.Db.TemplatesBiometricoFutronic.ToListAsync());
        c.Db.FotosCadastroFacial.Add(new FotoCadastroFacial { TrabalhadorId = c.Instrutor.Id, Conteudo = [1], HashSha256 = "h", CapturadaEm = DateTime.UtcNow });
        await c.Db.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, await c.IniciarAsync(c.A));
    }

    [Fact]
    public async Task Entrada_ListaDigitalEFacialDosInstrutores_EOsTemposDasFotosAutomaticas()
    {
        var c = await NovoCenarioAsync();
        c.Db.PlanosIntegracao.Add(new PlanoIntegracao { ObraId = c.Obra.Id, TrabalhadorId = c.A.Id, FuncaoId = c.Operador.Id, Status = StatusPlanoIntegracao.Aberto });
        await c.Db.SaveChangesAsync();

        var entrada = await new ObterEntradaIntegracaoQueryHandler(c.Db, Options.Create(c.Opcoes))
            .Handle(new ObterEntradaIntegracaoQuery(c.Obra.Id), default);

        var instrutor = Assert.Single(entrada.Instrutores);
        Assert.True(instrutor.TemDigital);
        Assert.False(instrutor.TemFacial);
        Assert.Equal(new FotosAutomaticasIntegracaoDto(60, 300, 300), entrada.FotosAutomaticas);
    }

    [Fact]
    public async Task Configuracao_FotosAutomaticasConfiguraveisEValoresInvalidosVoltamAoPadrao()
    {
        var opcoes = new OpcoesIntegracao { AtendimentoHabilitado = true };
        opcoes.FotosAutomaticas.PrimeiraAposSegundos = 90;
        opcoes.FotosAutomaticas.SegundaAposSegundos = 0;
        opcoes.FotosAutomaticas.TerceiraAposSegundos = 600;

        var dto = await new ObterConfiguracaoIntegracaoQueryHandler(Options.Create(opcoes)).Handle(new ObterConfiguracaoIntegracaoQuery(), default);

        Assert.True(dto.Habilitada);
        Assert.Equal(new FotosAutomaticasIntegracaoDto(90, 300, 600), dto.FotosAutomaticas);
    }

    // ---- Comandos de assinatura do instrutor ----

    [Fact]
    public async Task AssinarDigital_GuardaAProvaEDevolveAEtapaAssinada()
    {
        var c = await NovoCenarioAsync();
        var (id, etapa) = await EtapaEmAndamentoAsync(c);
        var leitor = new LeitorDigitalFalso();

        var dto = await new AssinarInstrutorEtapaPorDigitalCommandHandler(c.Db, leitor, new ClienteIntegracaoFalso())
            .Handle(new AssinarInstrutorEtapaPorDigitalCommand(id, etapa.Id, Digital(c.Instrutor.Id)), default);

        Assert.True(leitor.PermitiuOutraObra);
        Assert.True(dto.Etapas[0].InstrutorAssinou);
        Assert.Equal("biometria", dto.Etapas[0].InstrutorMetodo);
        Assert.DoesNotContain(dto.Etapas[0].Pendencias!, p => p.Codigo == "instrutor_assinatura");
        var guardada = await c.Db.AssinaturasInstrutorSessaoTreinamento.SingleAsync();
        Assert.Equal(c.Instrutor.Id, guardada.InstrutorTrabalhadorId);
        Assert.Equal(etapa.SessaoTreinamentoId, guardada.SessaoTreinamentoId);
        Assert.Equal(MetodoAutenticacaoAssinatura.Biometria, guardada.Metodo);
        Assert.Equal(64, guardada.EvidenciaHash!.Length);
        Assert.Equal("10.0.0.9", guardada.IpAddress);
        Assert.Equal("Teste", guardada.UserAgent);
        Assert.Equal(StatusLocalizacaoAssinatura.Capturada, guardada.LocalizacaoStatus);
        Assert.Equal(c.Obra.Id, guardada.ObraId);
    }

    [Fact]
    public async Task AssinarDigital_OutraPessoa_Recusa_ENadaEGuardado()
    {
        var c = await NovoCenarioAsync();
        var (id, etapa) = await EtapaEmAndamentoAsync(c);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.AssinarDigitalHandler()
            .Handle(new AssinarInstrutorEtapaPorDigitalCommand(id, etapa.Id, Digital(c.A.Id)), default));

        Assert.Equal("A digital lida não é a do instrutor desta integração.", ex.Message);
        Assert.Empty(await c.Db.AssinaturasInstrutorSessaoTreinamento.ToListAsync());
    }

    [Fact]
    public async Task AssinarDigital_AutenticacaoIdentificaOutraPessoa_Recusa()
    {
        var c = await NovoCenarioAsync();
        var (id, etapa) = await EtapaEmAndamentoAsync(c);
        var a = c.A.Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.AssinarDigitalHandler(_ => a)
            .Handle(new AssinarInstrutorEtapaPorDigitalCommand(id, etapa.Id, Digital(c.Instrutor.Id)), default));

        Assert.Contains("não é a do instrutor", ex.Message);
        Assert.Empty(await c.Db.AssinaturasInstrutorSessaoTreinamento.ToListAsync());
    }

    [Fact]
    public async Task AssinarDigital_Repetida_DaConflito()
    {
        var c = await NovoCenarioAsync();
        var (id, etapa) = await EtapaEmAndamentoAsync(c);
        var comando = new AssinarInstrutorEtapaPorDigitalCommand(id, etapa.Id, Digital(c.Instrutor.Id));
        await c.AssinarDigitalHandler().Handle(comando, default);

        var ex = await Assert.ThrowsAsync<AssinaturaInstrutorJaRegistradaException>(() => c.AssinarDigitalHandler().Handle(comando, default));

        Assert.Contains("já assinou", ex.Message);
        Assert.Single(await c.Db.AssinaturasInstrutorSessaoTreinamento.ToListAsync());
    }

    [Fact]
    public async Task AssinarDigital_EtapaConcluida_Recusa()
    {
        var c = await NovoCenarioAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        var etapa1 = await c.EtapaAsync(id, 1);
        await c.CompletarEtapaAtualAsync(id, 1);
        await c.AvancarAsync(id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.AssinarDigitalHandler()
            .Handle(new AssinarInstrutorEtapaPorDigitalCommand(id, etapa1.Id, Digital(c.Instrutor.Id)), default));

        Assert.Contains("etapa em andamento", ex.Message);
    }

    [Fact]
    public async Task AssinarDigital_EtapaDaOs_Recusa()
    {
        var c = await NovoCenarioAsync();
        var id = await c.IniciarDiretoNaOsAsync(c.A);
        var os = await c.EtapaAsync(id, 5);

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.AssinarDigitalHandler()
            .Handle(new AssinarInstrutorEtapaPorDigitalCommand(id, os.Id, Digital(c.Instrutor.Id)), default));
    }

    [Fact]
    public async Task AssinarDigital_SemAcessoAIntegracao_E404()
    {
        var c = await NovoCenarioAsync();
        var (id, etapa) = await EtapaEmAndamentoAsync(c);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => c.AssinarDigitalHandler().Handle(
            new AssinarInstrutorEtapaPorDigitalCommand(id, etapa.Id, Digital(c.Instrutor.Id), "oid-desconhecido", AutenticacaoHabilitada: true), default));
        Assert.Empty(await c.Db.AssinaturasInstrutorSessaoTreinamento.ToListAsync());
    }

    [Fact]
    public async Task AssinarFacial_BuscaNoGrupoDaObraDoInstrutor_EGuardaOMetodoFacial()
    {
        var c = await NovoCenarioAsync();
        // Instrutor de outra obra, autorizado a assinar fora dela.
        var outraObra = new Obra { Codigo = "OUT", Nome = "Outra obra" };
        c.Db.Obras.Add(outraObra);
        var instrutor = await c.Db.Trabalhadores.SingleAsync(t => t.Id == c.Instrutor.Id);
        instrutor.ObraId = outraObra.Id;
        var perfil = await c.Db.PerfisInstrutor.SingleAsync();
        perfil.PodeAssinarEmOutrasObras = true;
        perfil.AutorizadoOutrasObrasEm = DateTime.UtcNow;
        await c.Db.SaveChangesAsync();
        var (id, etapa) = await EtapaEmAndamentoAsync(c);
        var facial = new FacialIntegracaoFalso(() => c.Instrutor.Id);

        var dto = await c.AssinarFacialHandler(facial)
            .Handle(new AssinarInstrutorEtapaPorFacialCommand(id, etapa.Id, new DadosAssinaturaInstrutorFacial(Foto, "image/jpeg")), default);

        Assert.Equal(outraObra.Id, facial.ObraConsultada);
        Assert.NotEqual(c.Obra.Id, facial.ObraConsultada);
        Assert.Equal("reconhecimentoFacial", dto.Etapas[0].InstrutorMetodo);
        var guardada = await c.Db.AssinaturasInstrutorSessaoTreinamento.SingleAsync();
        Assert.Equal(MetodoAutenticacaoAssinatura.ReconhecimentoFacial, guardada.Metodo);
        Assert.Equal("req-1", guardada.ValidacaoRequisicaoId);
        Assert.Equal(Foto, guardada.EvidenciaConteudo);
    }

    [Fact]
    public async Task AssinarFacial_OutraPessoaOuRostoRejeitado_Recusa()
    {
        var c = await NovoCenarioAsync();
        var (id, etapa) = await EtapaEmAndamentoAsync(c);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.AssinarFacialHandler(new FacialIntegracaoFalso(() => c.A.Id))
            .Handle(new AssinarInstrutorEtapaPorFacialCommand(id, etapa.Id, new DadosAssinaturaInstrutorFacial(Foto)), default));
        Assert.Equal("A face lida não é a do instrutor desta integração.", ex.Message);

        await Assert.ThrowsAsync<RejeicaoFacialException>(() => c.AssinarFacialHandler(new FacialIntegracaoFalso(rejeitar: MotivoRejeicaoFacial.RostoNaoReconhecido))
            .Handle(new AssinarInstrutorEtapaPorFacialCommand(id, etapa.Id, new DadosAssinaturaInstrutorFacial(Foto)), default));
        Assert.Empty(await c.Db.AssinaturasInstrutorSessaoTreinamento.ToListAsync());
    }

    // ---- Encerramento: o certificado leva o instrutor por biometria, nunca pelo login ----

    [Fact]
    public async Task Encerrar_FacialDoInstrutor_VaiNoCertificadoComPapelInstrutorEMetodoFacial()
    {
        var c = await NovoCenarioAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        await c.CompletarEtapaAtualAsync(id, 1, comInstrutor: false);
        var etapa = await c.EtapaAsync(id, 1);
        await c.AssinarInstrutorAsync(etapa.SessaoTreinamentoId!.Value, MetodoAutenticacaoAssinatura.ReconhecimentoFacial);

        await c.AvancarAsync(id);

        var doInstrutor = await c.Db.DocumentoSignatarios.Where(s => s.TrabalhadorId == c.Instrutor.Id).ToListAsync();
        Assert.Equal(2, doInstrutor.Count);
        Assert.All(doInstrutor, s =>
        {
            Assert.Equal(PapelAssinatura.Instrutor, s.Papel);
            Assert.Equal(MetodoAutenticacaoAssinatura.ReconhecimentoFacial, s.MetodoAutenticacao);
        });
        Assert.DoesNotContain(await c.Db.DocumentoSignatarios.ToListAsync(), s => s.MetodoAutenticacao == MetodoAutenticacaoAssinatura.SessaoLogada);
    }

    [Fact]
    public async Task Encerrar_ComOidDoUsuarioLogado_NaoAssinaPeloLogin()
    {
        var c = await NovoCenarioAsync();
        var id = await c.IniciarAsync(c.A);
        await c.CompletarEtapaAtualAsync(id, 1);
        var etapa = await c.EtapaAsync(id, 1);
        var comandosLogin = 0;
        var anterior = c.RespostasExtras;
        c.RespostasExtras = (req, ct) =>
        {
            if (req is RegistrarAssinaturaSessaoLogadaCommand) comandosLogin++;
            return anterior is null ? throw new NotSupportedException(req.GetType().Name) : anterior(req, ct);
        };

        await c.EncerrarHandler().Handle(new EncerrarSessaoTreinamentoCommand(etapa.SessaoTreinamentoId!.Value, "oid-do-tecnico", ViaIntegracao: true), default);

        Assert.Equal(0, comandosLogin);
        Assert.DoesNotContain(await c.Db.DocumentoSignatarios.ToListAsync(), s => s.MetodoAutenticacao == MetodoAutenticacaoAssinatura.SessaoLogada);
    }

    [Fact]
    public async Task Encerrar_SemAssinaturaGuardada_RecusaAntesDeGravarQualquerCoisa()
    {
        var c = await NovoCenarioAsync();
        var id = await c.IniciarAsync(c.A);
        await c.CompletarEtapaAtualAsync(id, 1, comInstrutor: false);
        var etapa = await c.EtapaAsync(id, 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.EncerrarHandler()
            .Handle(new EncerrarSessaoTreinamentoCommand(etapa.SessaoTreinamentoId!.Value, null, ViaIntegracao: true), default));

        Assert.Contains("instrutor precisa assinar com biometria", ex.Message);
        Assert.Empty(await c.Db.Treinamentos.ToListAsync());
        Assert.Empty(await c.Db.DocumentosAssinatura.ToListAsync());
        Assert.Equal(StatusSessaoTreinamento.EmAndamento, (await c.Db.SessoesTreinamento.SingleAsync(s => s.Id == etapa.SessaoTreinamentoId)).Status);
    }

    private sealed class RegistradorQueFalhaParaInstrutor(IRegistradorAssinaturaService real) : IRegistradorAssinaturaService
    {
        public Task<DocumentoSignatarioDto> RegistrarAsync(
            Guid documentoAssinaturaId, ResultadoAutenticacaoAssinatura resultado, string? ipAddress, CancellationToken ct,
            byte[]? fotoEvidenciaConteudo = null, string? fotoEvidenciaContentType = null, PapelAssinatura? papel = null,
            LocalizacaoAssinatura? localizacao = null, string? userAgent = null)
        {
            if (papel == PapelAssinatura.Instrutor) throw new InvalidOperationException("falha simulada do registro do instrutor");
            return real.RegistrarAsync(documentoAssinaturaId, resultado, ipAddress, ct, fotoEvidenciaConteudo, fotoEvidenciaContentType, papel, localizacao, userAgent);
        }
    }

    [Fact]
    public async Task Encerrar_FalhaNoRegistroDoInstrutor_NaoFinalizaOCertificado_EMantemAEvidencia()
    {
        var c = await NovoCenarioAsync();
        var id = await c.IniciarAsync(c.A);
        await c.CompletarEtapaAtualAsync(id, 1);
        var etapa = await c.EtapaAsync(id, 1);
        var finalizacoes = new List<Guid>();
        c.RespostasExtras = (req, ct) =>
        {
            if (req is FinalizarDocumentoCommand f) finalizacoes.Add(f.DocumentoAssinaturaId);
            return Task.FromResult<object?>(null);
        };
        var registrador = new RegistradorQueFalhaParaInstrutor(new RegistradorAssinaturaService(c.Db, new AuditoriaIntegracaoFalsa(), new ClienteIntegracaoFalso()));
        var handler = new EncerrarSessaoTreinamentoCommandHandler(c.Db, c.Mediator, registrador, NullLogger<EncerrarSessaoTreinamentoCommandHandler>.Instance, c.Numeros);

        await handler.Handle(new EncerrarSessaoTreinamentoCommand(etapa.SessaoTreinamentoId!.Value, null, ViaIntegracao: true), default);

        Assert.Empty(finalizacoes);
        Assert.Single(await c.Db.Treinamentos.ToListAsync());
        Assert.DoesNotContain(await c.Db.DocumentoSignatarios.ToListAsync(), s => s.Papel == PapelAssinatura.Instrutor);
        // A prova segue guardada para nova tentativa.
        Assert.NotNull((await c.Db.AssinaturasInstrutorSessaoTreinamento.AsNoTracking().SingleAsync()).EvidenciaConteudo);
    }

    // ---- Turma comum (fora da integração) ----

    private sealed record TurmaComum(CenarioIntegracao Cenario, SessaoTreinamento Sessao, Trabalhador Participante);

    private static async Task<TurmaComum> TurmaComumAsync(bool comPresenca = true)
    {
        var c = await NovoCenarioAsync();
        var sessao = new SessaoTreinamento
        {
            ObraId = c.Obra.Id, CursoTreinamentoId = c.Nr35.Id, DataRealizacao = DateTime.UtcNow.Date, CargaHorariaRealizada = 8, ValidadeEmMeses = 24,
            InstrutorTrabalhadorId = c.Instrutor.Id, InstituicaoInstrutor = c.Instrutor.Nome,
        };
        sessao.Participantes.Add(new ParticipanteSessaoTreinamento
        {
            TrabalhadorId = c.A.Id,
            PresencaConfirmadaEm = comPresenca ? DateTime.UtcNow : null,
            MetodoPresenca = comPresenca ? MetodoAutenticacaoAssinatura.Biometria : null,
        });
        for (var i = 1; i <= 3; i++)
            sessao.FotosEvidencia.Add(new FotoEvidenciaSessaoTreinamento { Ordem = i, FotoConteudo = [9], FotoContentType = "image/jpeg" });
        c.Db.SessoesTreinamento.Add(sessao);
        await c.Db.SaveChangesAsync();
        return new TurmaComum(c, sessao, c.A);
    }

    [Fact]
    public async Task TurmaComum_EncerrarFicaBloqueadoAteOInstrutorAssinar()
    {
        var t = await TurmaComumAsync();
        var c = t.Cenario;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.EncerrarHandler().Handle(new EncerrarSessaoTreinamentoCommand(t.Sessao.Id), default));
        Assert.Contains("antes de encerrar esta turma", ex.Message);
        Assert.Empty(await c.Db.Treinamentos.ToListAsync());

        var assinatura = await new AssinarInstrutorTurmaPorDigitalCommandHandler(c.Db, new LeitorDigitalFalso(), new ClienteIntegracaoFalso())
            .Handle(new AssinarInstrutorTurmaPorDigitalCommand(t.Sessao.Id, Digital(c.Instrutor.Id)), default);
        Assert.Equal("biometria", assinatura.Metodo);

        await c.EncerrarHandler().Handle(new EncerrarSessaoTreinamentoCommand(t.Sessao.Id), default);

        Assert.Single(await c.Db.Treinamentos.ToListAsync());
        Assert.Contains(await c.Db.DocumentoSignatarios.ToListAsync(), s => s.Papel == PapelAssinatura.Instrutor && s.TrabalhadorId == c.Instrutor.Id);
        Assert.DoesNotContain(await c.Db.DocumentoSignatarios.ToListAsync(), s => s.MetodoAutenticacao == MetodoAutenticacaoAssinatura.SessaoLogada);
    }

    [Fact]
    public async Task TurmaComum_MensagensFalamTurma_ERepetidaDaConflito()
    {
        var t = await TurmaComumAsync();
        var c = t.Cenario;
        var handler = new AssinarInstrutorTurmaPorDigitalCommandHandler(c.Db, new LeitorDigitalFalso(), new ClienteIntegracaoFalso());

        var outra = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new AssinarInstrutorTurmaPorDigitalCommand(t.Sessao.Id, Digital(c.A.Id)), default));
        Assert.Equal("A digital lida não é a do instrutor desta turma.", outra.Message);

        await handler.Handle(new AssinarInstrutorTurmaPorDigitalCommand(t.Sessao.Id, Digital(c.Instrutor.Id)), default);
        var repetida = await Assert.ThrowsAsync<AssinaturaInstrutorJaRegistradaException>(() =>
            handler.Handle(new AssinarInstrutorTurmaPorDigitalCommand(t.Sessao.Id, Digital(c.Instrutor.Id)), default));
        Assert.Equal("O instrutor já assinou esta turma.", repetida.Message);
    }

    [Fact]
    public async Task TurmaComum_FacialDoInstrutor_AssinaEEncerra()
    {
        var t = await TurmaComumAsync();
        var c = t.Cenario;
        var facial = new FacialIntegracaoFalso(() => c.Instrutor.Id);

        await new AssinarInstrutorTurmaPorFacialCommandHandler(c.Db, facial, new ClienteIntegracaoFalso())
            .Handle(new AssinarInstrutorTurmaPorFacialCommand(t.Sessao.Id, new DadosAssinaturaInstrutorFacial(Foto, "image/jpeg")), default);
        await c.EncerrarHandler().Handle(new EncerrarSessaoTreinamentoCommand(t.Sessao.Id), default);

        Assert.Equal(c.Obra.Id, facial.ObraConsultada);
        var assinatura = await c.Db.DocumentoSignatarios.SingleAsync(s => s.Papel == PapelAssinatura.Instrutor);
        Assert.Equal(MetodoAutenticacaoAssinatura.ReconhecimentoFacial, assinatura.MetodoAutenticacao);
    }

    [Fact]
    public async Task TurmaComum_TrocarOInstrutorDepoisDaAssinatura_Recusa()
    {
        var t = await TurmaComumAsync();
        var c = t.Cenario;
        var outro = await c.NovaPessoaAsync(c.Ajudante, "Outro Instrutor");
        c.Db.PerfisInstrutor.Add(new PerfilInstrutor { TrabalhadorId = outro.Id });
        await c.Db.SaveChangesAsync();
        await c.AssinarInstrutorAsync(t.Sessao.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new DefinirInstrutorSessaoTreinamentoCommandHandler(c.Db)
            .Handle(new DefinirInstrutorSessaoTreinamentoCommand(t.Sessao.Id, outro.Id), default));
        Assert.Contains("já assinou", ex.Message);
    }

    [Fact]
    public async Task TurmaJaEncerrada_NaoMudaCertificadosEmitidos_NemAceitaNovaAssinaturaPrevia()
    {
        var t = await TurmaComumAsync();
        var c = t.Cenario;
        // Turma encerrada no modelo antigo: certificado emitido com a assinatura de quem encerrou (sessão logada), sem assinatura guardada.
        var sessao = await c.Db.SessoesTreinamento.SingleAsync(s => s.Id == t.Sessao.Id);
        sessao.Status = StatusSessaoTreinamento.Concluida;
        var treinamento = new Treinamento { TrabalhadorId = t.Participante.Id, CursoTreinamentoId = c.Nr35.Id, NumeroCertificado = "AAH-NR35-2026-0001" };
        c.Db.Treinamentos.Add(treinamento);
        var documento = new DocumentoAssinatura { EntidadeTipo = nameof(Treinamento), EntidadeId = treinamento.Id, Status = StatusDocumentoAssinatura.Finalizado, ConteudoHash = "hash-antigo" };
        documento.Signatarios.Add(new DocumentoSignatario { TrabalhadorId = c.Instrutor.Id, MetodoAutenticacao = MetodoAutenticacaoAssinatura.SessaoLogada, AssinadoEm = DateTime.UtcNow });
        c.Db.DocumentosAssinatura.Add(documento);
        await c.Db.SaveChangesAsync();

        var encerrar = await Assert.ThrowsAsync<InvalidOperationException>(() => c.EncerrarHandler().Handle(new EncerrarSessaoTreinamentoCommand(sessao.Id), default));
        Assert.Contains("já foi encerrada", encerrar.Message);
        var assinar = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AssinarInstrutorTurmaPorDigitalCommandHandler(c.Db, new LeitorDigitalFalso(), new ClienteIntegracaoFalso())
                .Handle(new AssinarInstrutorTurmaPorDigitalCommand(sessao.Id, Digital(c.Instrutor.Id)), default));
        Assert.Contains("já foi encerrada", assinar.Message);

        var doc = await c.Db.DocumentosAssinatura.AsNoTracking().Include(d => d.Signatarios).SingleAsync();
        Assert.Equal(StatusDocumentoAssinatura.Finalizado, doc.Status);
        Assert.Equal("hash-antigo", doc.ConteudoHash);
        var unica = Assert.Single(doc.Signatarios);
        Assert.Equal(MetodoAutenticacaoAssinatura.SessaoLogada, unica.MetodoAutenticacao);
        Assert.Empty(await c.Db.AssinaturasInstrutorSessaoTreinamento.ToListAsync());
        Assert.Single(await c.Db.Treinamentos.ToListAsync());
    }
}
