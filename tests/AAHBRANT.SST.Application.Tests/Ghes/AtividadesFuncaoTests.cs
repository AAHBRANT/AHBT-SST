using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Ghes.AtividadesFuncao;
using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Ghes;

// Atividades por função na OS (Entrega 4, 10/11/2026): a IA sugere, o Engenheiro de Segurança decide e a
// OS lista só as atividades confirmadas para a função do trabalhador.
public class AtividadesFuncaoTests
{
    private static readonly Guid ObraA = Guid.NewGuid();
    private static readonly Guid ObraB = Guid.NewGuid();

    private sealed class AuditoriaFalsa : IAuditoriaService
    {
        public List<(string Acao, Guid EntidadeId, Guid? UsuarioId)> Registros { get; } = new();

        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
        {
            Registros.Add((acao, entidadeId, usuarioId));
            return Task.CompletedTask;
        }
    }

    private sealed class AnalistaFalso : IAnalistaAtividadesFuncao
    {
        private readonly List<SugestaoFuncaoIa> _resposta;
        public List<ContextoAtividadesFuncao> Contextos { get; } = new();
        public AnalistaFalso(params SugestaoFuncaoIa[] resposta) => _resposta = resposta.ToList();

        public Task<ResultadoAnaliseAtividadesFuncao> SugerirAsync(ContextoAtividadesFuncao contexto, CancellationToken ct)
        {
            Contextos.Add(contexto);
            return Task.FromResult(new ResultadoAnaliseAtividadesFuncao(_resposta, 100, 20));
        }
    }

    private sealed record Cenario(
        SstDbContext Global, SstDbContext RestritoObraA, Ghe Ghe, Funcao Armador, Funcao Pedreiro,
        Atividade Corte, Atividade Desforma, Atividade Montagem, Atividade DeOutraObra, Trabalhador Trabalhador);

    private static async Task<Cenario> CriarAsync()
    {
        ChavesCpfDeTeste.Configurar();
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var global = new SstDbContext(opcoes, new CurrentUserService());
        var restritoUsuario = new CurrentUserService();
        restritoUsuario.DefinirEscopo(false, new[] { ObraA });
        var restrito = new SstDbContext(opcoes, restritoUsuario);

        var obraA = new Obra { Id = ObraA, Codigo = "OB-A", Nome = "Obra A" };
        var obraB = new Obra { Id = ObraB, Codigo = "OB-B", Nome = "Obra B" };
        var armador = new Funcao { Nome = "Armador" };
        var pedreiro = new Funcao { Nome = "Pedreiro" };
        var ghe = new Ghe { ObraId = ObraA, Numero = 3 };
        var gheB = new Ghe { ObraId = ObraB, Numero = 1 };
        var corte = new Atividade { ObraId = ObraA, GheId = ghe.Id, Nome = "Corte" };
        var desforma = new Atividade { ObraId = ObraA, GheId = ghe.Id, Nome = "Desforma" };
        var montagem = new Atividade { ObraId = ObraA, GheId = ghe.Id, Nome = "Montagem" };
        var deOutraObra = new Atividade { ObraId = ObraB, GheId = gheB.Id, Nome = "Alvenaria" };
        var trabalhador = new Trabalhador { ObraId = ObraA, Funcao = armador, Nome = "Trabalhador", Cpf = "00000000001", DataAdmissao = DateTime.UtcNow.AddYears(-1) };
        global.AddRange(obraA, obraB, armador, pedreiro, ghe, gheB, corte, desforma, montagem, deOutraObra, trabalhador);
        global.GheFuncoes.AddRange(
            new GheFuncao { GheId = ghe.Id, FuncaoId = armador.Id, DescricaoAtividades = "Corta e monta armadura" },
            new GheFuncao { GheId = gheB.Id, FuncaoId = armador.Id });
        await global.SaveChangesAsync();
        return new Cenario(global, restrito, ghe, armador, pedreiro, corte, desforma, montagem, deOutraObra, trabalhador);
    }

    private static DecidirAtividadesFuncaoCommandHandler Decisor(SstDbContext db, AuditoriaFalsa? auditoria = null)
        => new(db, auditoria ?? new AuditoriaFalsa());

    private static Task Decidir(Cenario c, params (Atividade A, bool Executa)[] itens)
        => Decisor(c.Global).Handle(new DecidirAtividadesFuncaoCommand(ObraA,
            itens.Select(i => new DecisaoAtividadeFuncao(i.A.Id, c.Armador.Id, i.Executa)).ToList(), Guid.NewGuid(), "Engenheiro", false), default);

    private static Task<MinutaOrdemServicoDto?> Minuta(Cenario c)
        => new ObterMinutaOrdemServicoQueryHandler(c.Global).Handle(new ObterMinutaOrdemServicoQuery(c.Trabalhador.Id), default);

    // ---- Decidir ----

    [Fact]
    public async Task Decidir_GravaStatusQuemDecidiu_CriaComoManual_EAudita()
    {
        var c = await CriarAsync();
        var auditoria = new AuditoriaFalsa();
        var usuario = Guid.NewGuid();

        var n = await Decisor(c.Global, auditoria).Handle(new DecidirAtividadesFuncaoCommand(ObraA, new()
        {
            new(c.Corte.Id, c.Armador.Id, true),
            new(c.Desforma.Id, c.Armador.Id, false),
        }, usuario, "Engenheiro Fulano", false), default);

        Assert.Equal(2, n);
        var corte = await c.Global.AtividadesFuncoes.SingleAsync(x => x.AtividadeId == c.Corte.Id);
        Assert.Equal(StatusMedidaPrevencao.Validada, corte.Status);
        Assert.Equal(OrigemMedidaPrevencao.Manual, corte.OrigemVinculo);
        Assert.Equal(usuario, corte.ValidadoPorUsuarioId);
        Assert.Equal("Engenheiro Fulano", corte.ValidadoPorNome);
        Assert.NotNull(corte.ValidadoEmUtc);
        Assert.Equal(StatusMedidaPrevencao.Descartada, (await c.Global.AtividadesFuncoes.SingleAsync(x => x.AtividadeId == c.Desforma.Id)).Status);
        var trilha = Assert.Single(auditoria.Registros);
        Assert.Equal("AtividadeFuncao.Decidida", trilha.Acao);
        Assert.Equal(usuario, trilha.UsuarioId);
    }

    [Fact]
    public async Task Decidir_SugestaoDaIa_MantemOrigemEViraValidada()
    {
        var c = await CriarAsync();
        c.Global.AtividadesFuncoes.Add(new AtividadeFuncao
        {
            ObraId = ObraA, AtividadeId = c.Corte.Id, FuncaoId = c.Armador.Id,
            Status = StatusMedidaPrevencao.Pendente, OrigemVinculo = OrigemMedidaPrevencao.SugestaoIa, JustificativaIa = "Corta aço",
        });
        await c.Global.SaveChangesAsync();

        await Decidir(c, (c.Corte, true));

        var r = await c.Global.AtividadesFuncoes.SingleAsync();
        Assert.Equal(StatusMedidaPrevencao.Validada, r.Status);
        Assert.Equal(OrigemMedidaPrevencao.SugestaoIa, r.OrigemVinculo);
        Assert.Equal("Corta aço", r.JustificativaIa);
    }

    [Fact]
    public async Task Decidir_AtividadeDeOutraObraNoEscopoRestrito_NaoEncontrada()
    {
        var c = await CriarAsync();

        // Usuário restrito à obra A tentando decidir atividade da obra B (obra B na rota)...
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Decisor(c.RestritoObraA).Handle(
            new DecidirAtividadesFuncaoCommand(ObraB, new() { new(c.DeOutraObra.Id, c.Armador.Id, true) }, Guid.NewGuid(), "x", false), default));
        // ... ou com a obra A na rota e a atividade da B no corpo.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Decisor(c.RestritoObraA).Handle(
            new DecidirAtividadesFuncaoCommand(ObraA, new() { new(c.DeOutraObra.Id, c.Armador.Id, true) }, Guid.NewGuid(), "x", false), default));
        Assert.Empty(await c.Global.AtividadesFuncoes.ToListAsync());
    }

    [Fact]
    public async Task Decidir_AtividadeInexistenteOuDeOutraObraRotaErrada_NaoEncontrada_MesmoComEscopoGlobal()
    {
        var c = await CriarAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Decisor(c.Global).Handle(
            new DecidirAtividadesFuncaoCommand(ObraA, new() { new(c.DeOutraObra.Id, c.Armador.Id, true) }, null, null, false), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Decisor(c.Global).Handle(
            new DecidirAtividadesFuncaoCommand(ObraA, new() { new(Guid.NewGuid(), c.Armador.Id, true) }, null, null, false), default));
    }

    [Fact]
    public async Task Decidir_FuncaoForaDoGhe_Erro()
    {
        var c = await CriarAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => Decisor(c.Global).Handle(
            new DecidirAtividadesFuncaoCommand(ObraA, new() { new(c.Corte.Id, c.Pedreiro.Id, true) }, null, null, false), default));

        Assert.Contains("não pertence ao GHE", erro.Message);
        Assert.Empty(await c.Global.AtividadesFuncoes.ToListAsync());
    }

    [Fact]
    public async Task Decidir_MesmaDecisaoDeNovo_NaoRegravaNemAudita()
    {
        var c = await CriarAsync();
        await Decidir(c, (c.Corte, true));
        var primeira = (await c.Global.AtividadesFuncoes.SingleAsync()).ValidadoEmUtc;
        var auditoria = new AuditoriaFalsa();

        var n = await Decisor(c.Global, auditoria).Handle(new DecidirAtividadesFuncaoCommand(ObraA,
            new() { new(c.Corte.Id, c.Armador.Id, true) }, Guid.NewGuid(), "Outro", false), default);

        Assert.Equal(0, n);
        Assert.Empty(auditoria.Registros);
        Assert.Equal(primeira, (await c.Global.AtividadesFuncoes.SingleAsync()).ValidadoEmUtc);
    }

    // ---- Sugerir com IA ----

    [Fact]
    public async Task Sugerir_GravaPendente_EDescartaCodigoEFuncaoInventados()
    {
        var c = await CriarAsync();
        var analista = new AnalistaFalso(
            new SugestaoFuncaoIa("F01", new()
            {
                new("A001", "Corta aço para a armadura"),
                new("A999", "Código inventado"),
            }),
            new SugestaoFuncaoIa("F09", new() { new("A002", "Função inventada") }));

        var r = await new SugerirAtividadesFuncaoGheCommandHandler(c.Global, analista)
            .Handle(new SugerirAtividadesFuncaoGheCommand(ObraA, c.Ghe.Id), default);

        Assert.Equal(1, r.Sugeridos);
        Assert.Equal(1, r.ChamadasIa);
        Assert.Equal(100, r.TokensEntrada);
        var gravado = await c.Global.AtividadesFuncoes.SingleAsync();
        Assert.Equal(c.Corte.Id, gravado.AtividadeId);
        Assert.Equal(c.Armador.Id, gravado.FuncaoId);
        Assert.Equal(StatusMedidaPrevencao.Pendente, gravado.Status);
        Assert.Equal(OrigemMedidaPrevencao.SugestaoIa, gravado.OrigemVinculo);
        Assert.Equal("Corta aço para a armadura", gravado.JustificativaIa);
        Assert.Null(gravado.ValidadoPorNome);
        // A IA recebe só as funções do GHE, com a descrição do PGR, e as atividades por código.
        var contexto = Assert.Single(analista.Contextos);
        var funcaoEnviada = Assert.Single(contexto.Funcoes);
        Assert.Equal("F01", funcaoEnviada.Codigo);
        Assert.Equal("Armador", funcaoEnviada.Nome);
        Assert.Equal("Corta e monta armadura", funcaoEnviada.DescricaoNoPgr);
        Assert.Equal(new[] { "A001", "A002", "A003" }, contexto.Atividades.Select(a => a.Codigo));
    }

    [Fact]
    public async Task Sugerir_NaoSobrescreveValidadaNemDescartada_NemDuplicaPendente()
    {
        var c = await CriarAsync();
        c.Global.AtividadesFuncoes.AddRange(
            new AtividadeFuncao { ObraId = ObraA, AtividadeId = c.Corte.Id, FuncaoId = c.Armador.Id, Status = StatusMedidaPrevencao.Validada, OrigemVinculo = OrigemMedidaPrevencao.Manual, ValidadoPorNome = "Eng" },
            new AtividadeFuncao { ObraId = ObraA, AtividadeId = c.Desforma.Id, FuncaoId = c.Armador.Id, Status = StatusMedidaPrevencao.Descartada, OrigemVinculo = OrigemMedidaPrevencao.Manual, ValidadoPorNome = "Eng" });
        await c.Global.SaveChangesAsync();
        // A IA tenta marcar tudo, inclusive o que já foi decidido.
        var analista = new AnalistaFalso(new SugestaoFuncaoIa("F01", new() { new("A001", "x"), new("A002", "y"), new("A003", "z"), new("A003", "repetida") }));

        var r = await new SugerirAtividadesFuncaoGheCommandHandler(c.Global, analista)
            .Handle(new SugerirAtividadesFuncaoGheCommand(ObraA, c.Ghe.Id), default);

        Assert.Equal(1, r.Sugeridos);
        Assert.Equal(2, r.PreexistentesIgnorados);
        var todas = await c.Global.AtividadesFuncoes.ToListAsync();
        Assert.Equal(3, todas.Count);
        Assert.Equal(StatusMedidaPrevencao.Validada, todas.Single(x => x.AtividadeId == c.Corte.Id).Status);
        Assert.Equal(StatusMedidaPrevencao.Descartada, todas.Single(x => x.AtividadeId == c.Desforma.Id).Status);
        Assert.Equal(StatusMedidaPrevencao.Pendente, todas.Single(x => x.AtividadeId == c.Montagem.Id).Status);
    }

    [Fact]
    public async Task Sugerir_TudoJaDecidido_NaoChamaAIa()
    {
        var c = await CriarAsync();
        foreach (var a in new[] { c.Corte, c.Desforma, c.Montagem })
            c.Global.AtividadesFuncoes.Add(new AtividadeFuncao { ObraId = ObraA, AtividadeId = a.Id, FuncaoId = c.Armador.Id, Status = StatusMedidaPrevencao.Descartada });
        await c.Global.SaveChangesAsync();
        var analista = new AnalistaFalso();

        var r = await new SugerirAtividadesFuncaoGheCommandHandler(c.Global, analista)
            .Handle(new SugerirAtividadesFuncaoGheCommand(ObraA, c.Ghe.Id), default);

        Assert.Equal(0, r.ChamadasIa);
        Assert.Empty(analista.Contextos);
    }

    [Fact]
    public async Task Sugerir_GheDeOutraObraOuForaDoEscopo_NaoEncontrado()
    {
        var c = await CriarAsync();
        var analista = new AnalistaFalso();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new SugerirAtividadesFuncaoGheCommandHandler(c.Global, analista)
            .Handle(new SugerirAtividadesFuncaoGheCommand(ObraB, c.Ghe.Id), default));
        var gheB = await c.Global.Ghes.SingleAsync(g => g.ObraId == ObraB);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new SugerirAtividadesFuncaoGheCommandHandler(c.RestritoObraA, analista)
            .Handle(new SugerirAtividadesFuncaoGheCommand(ObraB, gheB.Id), default));
    }

    [Fact]
    public async Task Sugerir_MaisDe80Atividades_DivideEmChamadas()
    {
        var c = await CriarAsync();
        for (var i = 0; i < 90; i++)
            c.Global.Atividades.Add(new Atividade { ObraId = ObraA, GheId = c.Ghe.Id, Nome = $"Extra {i:00}" });
        await c.Global.SaveChangesAsync();
        var analista = new AnalistaFalso();

        var r = await new SugerirAtividadesFuncaoGheCommandHandler(c.Global, analista)
            .Handle(new SugerirAtividadesFuncaoGheCommand(ObraA, c.Ghe.Id), default);

        Assert.Equal(2, r.ChamadasIa);
        Assert.All(analista.Contextos, x => Assert.True(x.Atividades.Count <= SugerirAtividadesFuncaoGheCommandHandler.MaxAtividadesPorChamada));
    }

    // ---- Matriz ----

    [Fact]
    public async Task Matriz_TrazTodosOsPares_ContadoresEProcedimentos()
    {
        var c = await CriarAsync();
        var doc = new MaterialApoio { Nome = "Procedimento de corte", Tipo = TipoDocumentoSst.Procedimento, Situacao = SituacaoDocumentoSst.Vigente, Codigo = "PRO-01", Revisao = "1" };
        c.Global.MateriaisApoio.Add(doc);
        c.Global.AtividadeDocumentos.Add(new AtividadeDocumento { AtividadeId = c.Corte.Id, MaterialApoioId = doc.Id });
        c.Global.AtividadesFuncoes.AddRange(
            new AtividadeFuncao { ObraId = ObraA, AtividadeId = c.Corte.Id, FuncaoId = c.Armador.Id, Status = StatusMedidaPrevencao.Validada, ValidadoPorNome = "Eng" },
            new AtividadeFuncao { ObraId = ObraA, AtividadeId = c.Desforma.Id, FuncaoId = c.Armador.Id, Status = StatusMedidaPrevencao.Pendente, OrigemVinculo = OrigemMedidaPrevencao.SugestaoIa, JustificativaIa = "ok" });
        await c.Global.SaveChangesAsync();

        var m = await new ObterMatrizAtividadesFuncaoGheQueryHandler(c.Global).Handle(new ObterMatrizAtividadesFuncaoGheQuery(ObraA, c.Ghe.Id), default);

        Assert.NotNull(m);
        Assert.Equal(3, m!.GheNumero);
        Assert.Equal(3, m.Atividades.Count);
        Assert.Equal(1, m.Atividades.Single(a => a.AtividadeId == c.Corte.Id).Procedimentos);
        Assert.Equal(0, m.Atividades.Single(a => a.AtividadeId == c.Desforma.Id).Procedimentos);
        var f = Assert.Single(m.Funcoes);
        Assert.Equal("Armador", f.Nome);
        Assert.Equal((1, 1, 0, 1), (f.Validadas, f.Pendentes, f.Descartadas, f.SemDecisao));
        Assert.Equal(3, m.Celulas.Count);
        Assert.Equal(EstadoAtividadeFuncao.SemDecisao, m.Celulas.Single(x => x.AtividadeId == c.Montagem.Id).Estado);
        Assert.Equal("ok", m.Celulas.Single(x => x.AtividadeId == c.Desforma.Id).JustificativaIa);
    }

    [Fact]
    public async Task Matriz_FuncaoExcluida_NaoSomeDaMatriz()
    {
        var c = await CriarAsync();
        c.Armador.Ativo = false;
        await c.Global.SaveChangesAsync();

        var m = await new ObterMatrizAtividadesFuncaoGheQueryHandler(c.Global).Handle(new ObterMatrizAtividadesFuncaoGheQuery(ObraA, c.Ghe.Id), default);

        Assert.Single(m!.Funcoes);
    }

    // ---- Minuta da OS ----

    [Fact]
    public async Task Minuta_SemNenhumaDecisao_TrazTodasComoPrevia_ComUmaPendenciaSo()
    {
        var c = await CriarAsync();

        var m = (await Minuta(c))!;

        Assert.Equal(3, m.Atividades.Count);
        Assert.False(m.AtividadesDaFuncaoConfirmadas);
        var pendencia = Assert.Single(m.Pendencias, p => p.Tipo == "AtividadesFuncaoNaoConfirmadas");
        Assert.Contains("função Armador no GHE 03", pendencia.Titulo);
        Assert.DoesNotContain(m.Pendencias, p => p.Tipo == "SemProcedimento");
        Assert.False(m.PodeEmitir);
    }

    [Fact]
    public async Task Minuta_ComDecisoes_ListaSoAsConfirmadas()
    {
        var c = await CriarAsync();
        await Decidir(c, (c.Corte, true), (c.Desforma, false), (c.Montagem, false));

        var m = (await Minuta(c))!;

        var atividade = Assert.Single(m.Atividades);
        Assert.Equal("Corte", atividade.Nome);
        Assert.True(m.AtividadesDaFuncaoConfirmadas);
        Assert.DoesNotContain(m.Pendencias, p => p.Tipo is "AtividadesFuncaoNaoConfirmadas" or "AtividadesFuncaoSemDecisao");
        // Regra de sempre, agora só para a atividade da função: sem procedimento bloqueia.
        var semProcedimento = Assert.Single(m.Pendencias, p => p.Tipo == "SemProcedimento");
        Assert.Contains("Corte", semProcedimento.Titulo);
    }

    [Fact]
    public async Task Minuta_DecisaoParcial_AtividadeSemDecisaoViraPendenciaAgrupada()
    {
        var c = await CriarAsync();
        await Decidir(c, (c.Corte, true));
        // Sugestão da IA ainda pendente não é decisão.
        c.Global.AtividadesFuncoes.Add(new AtividadeFuncao { ObraId = ObraA, AtividadeId = c.Desforma.Id, FuncaoId = c.Armador.Id, Status = StatusMedidaPrevencao.Pendente, OrigemVinculo = OrigemMedidaPrevencao.SugestaoIa });
        await c.Global.SaveChangesAsync();

        var m = (await Minuta(c))!;

        Assert.Equal("Corte", Assert.Single(m.Atividades).Nome);
        Assert.False(m.AtividadesDaFuncaoConfirmadas);
        var agrupada = Assert.Single(m.Pendencias, p => p.Tipo == "AtividadesFuncaoSemDecisao");
        Assert.Contains("Desforma", agrupada.Titulo);
        Assert.Contains("Montagem", agrupada.Titulo);
        Assert.DoesNotContain("Corte", agrupada.Titulo);
    }

    [Fact]
    public async Task Minuta_TodasDescartadas_SemAtividadesComTextoProprio()
    {
        var c = await CriarAsync();
        await Decidir(c, (c.Corte, false), (c.Desforma, false), (c.Montagem, false));

        var m = (await Minuta(c))!;

        Assert.Empty(m.Atividades);
        Assert.True(m.AtividadesDaFuncaoConfirmadas);
        var p = Assert.Single(m.Pendencias, x => x.Tipo == "SemAtividades");
        Assert.Equal("Nenhuma atividade confirmada para a função Armador", p.Titulo);
    }

    [Fact]
    public void ConteudoJsonAntigo_SemOCampoNovo_ContinuaConfirmado()
    {
        var antigo = System.Text.Json.JsonSerializer.Deserialize<MinutaOrdemServicoDto>("{\"TrabalhadorId\":\"" + Guid.NewGuid() + "\",\"Atividades\":[]}")!;
        Assert.True(antigo.AtividadesDaFuncaoConfirmadas);
    }

    // ---- Engenheiro na obra da rota ----

    private static async Task<Guid> VincularAsync(Cenario c, TipoPerfilAcesso tipo, params Guid?[] obras)
    {
        var usuario = new Usuario { Nome = "Fulano", Email = $"{Guid.NewGuid()}@x.com", Status = StatusUsuario.Ativo };
        c.Global.Usuarios.Add(usuario);
        await c.Global.SaveChangesAsync();
        foreach (var obra in obras)
        {
            var perfil = new PerfilAcesso { Tipo = tipo, Nome = tipo.ToString(), EhSistema = true };
            c.Global.PerfisAcesso.Add(perfil);
            await c.Global.SaveChangesAsync();
            c.Global.UsuariosPerfilObra.Add(new UsuarioPerfilObra { UsuarioId = usuario.Id, PerfilAcessoId = perfil.Id, ObraId = obra });
        }
        await c.Global.SaveChangesAsync();
        return usuario.Id;
    }

    private static Task<int> DecidirComo(Cenario c, Guid? usuario, Guid obra, Atividade a)
        => Decisor(c.Global).Handle(new DecidirAtividadesFuncaoCommand(obra, new() { new(a.Id, c.Armador.Id, true) }, usuario, "Eng", true), default);

    [Fact]
    public async Task Decidir_EngenheiroNaObraANaoDecideNaObraBOndeETecnico()
    {
        var c = await CriarAsync();
        var usuario = await VincularAsync(c, TipoPerfilAcesso.EngenheiroSeguranca, ObraA);
        var tecnico = new PerfilAcesso { Tipo = TipoPerfilAcesso.TecnicoSeguranca, Nome = "Técnico", EhSistema = true };
        c.Global.PerfisAcesso.Add(tecnico);
        await c.Global.SaveChangesAsync();
        c.Global.UsuariosPerfilObra.Add(new UsuarioPerfilObra { UsuarioId = usuario, ObraId = ObraB, PerfilAcessoId = tecnico.Id });
        await c.Global.SaveChangesAsync();

        var erro = await Assert.ThrowsAsync<AAHBRANT.SST.Application.Common.Seguranca.AcessoNegadoException>(() => DecidirComo(c, usuario, ObraB, c.DeOutraObra));

        Assert.Contains("Engenheiro de Segurança desta obra", erro.Message);
        Assert.Empty(await c.Global.AtividadesFuncoes.ToListAsync());
        // Na obra A, onde é engenheiro, decide normalmente.
        Assert.Equal(1, await DecidirComo(c, usuario, ObraA, c.Corte));
    }

    [Fact]
    public async Task Decidir_EngenheiroDaObraBDecideNaB_EGlobalDecideEmQualquer()
    {
        var c = await CriarAsync();
        var daB = await VincularAsync(c, TipoPerfilAcesso.EngenheiroSeguranca, ObraB);
        var global = await VincularAsync(c, TipoPerfilAcesso.EngenheiroSeguranca, (Guid?)null);

        Assert.Equal(1, await DecidirComo(c, daB, ObraB, c.DeOutraObra));
        Assert.Equal(1, await DecidirComo(c, global, ObraA, c.Corte));
        await Assert.ThrowsAsync<AAHBRANT.SST.Application.Common.Seguranca.AcessoNegadoException>(() => DecidirComo(c, daB, ObraA, c.Desforma));
    }

    [Fact]
    public async Task Decidir_SemUsuarioOuAdministrador_NegadoQuandoExigeEngenheiro()
    {
        var c = await CriarAsync();
        var admin = await VincularAsync(c, TipoPerfilAcesso.Administrador, (Guid?)null);

        await Assert.ThrowsAsync<AAHBRANT.SST.Application.Common.Seguranca.AcessoNegadoException>(() => DecidirComo(c, null, ObraA, c.Corte));
        await Assert.ThrowsAsync<AAHBRANT.SST.Application.Common.Seguranca.AcessoNegadoException>(() => DecidirComo(c, admin, ObraA, c.Corte));
    }

    // ---- Casos da OS ----

    [Fact]
    public async Task Minuta_FuncaoEmDoisGhesComDecisaoSoEmUm_Bloqueia()
    {
        var c = await CriarAsync();
        var ghe2 = new Ghe { ObraId = ObraA, Numero = 4 };
        var emGhe2 = new Atividade { ObraId = ObraA, GheId = ghe2.Id, Nome = "Estaqueamento" };
        c.Global.AddRange(ghe2, emGhe2);
        c.Global.GheFuncoes.Add(new GheFuncao { GheId = ghe2.Id, FuncaoId = c.Armador.Id });
        await c.Global.SaveChangesAsync();
        await Decidir(c, (c.Corte, true), (c.Desforma, false), (c.Montagem, false));

        var m = (await Minuta(c))!;

        Assert.False(m.PodeEmitir);
        Assert.False(m.AtividadesDaFuncaoConfirmadas);
        var p = Assert.Single(m.Pendencias, x => x.Tipo == "AtividadesFuncaoSemDecisao");
        Assert.Contains("Estaqueamento", p.Titulo);
    }

    [Fact]
    public async Task Minuta_AtividadeNovaNoGheDepoisDaConfirmacao_Bloqueia()
    {
        var c = await CriarAsync();
        await Decidir(c, (c.Corte, true), (c.Desforma, false), (c.Montagem, false));
        Assert.True((await Minuta(c))!.AtividadesDaFuncaoConfirmadas);
        c.Global.Atividades.Add(new Atividade { ObraId = ObraA, GheId = c.Ghe.Id, Nome = "Concretagem nova" });
        await c.Global.SaveChangesAsync();

        var m = (await Minuta(c))!;

        Assert.False(m.PodeEmitir);
        Assert.Contains("Concretagem nova", Assert.Single(m.Pendencias, x => x.Tipo == "AtividadesFuncaoSemDecisao").Titulo);
    }

    [Fact]
    public async Task Minuta_DecisoesSemNenhumaValidada_ComAtividadeSemDecisao_NaoDizInventarioVazio()
    {
        var c = await CriarAsync();
        await Decidir(c, (c.Corte, false), (c.Desforma, false));

        var m = (await Minuta(c))!;

        Assert.Empty(m.Atividades);
        Assert.Contains(m.Pendencias, x => x.Tipo == "AtividadesFuncaoSemDecisao");
        Assert.DoesNotContain(m.Pendencias, x => x.Tipo == "SemAtividades");
    }

    // ---- Emissão: OS aguardando assinatura vem antes das pendências ----

    private sealed class NumeroFalso : IGeradorNumeroDocumentoService
    {
        public Task<string> GerarAsync(string prefixo, CancellationToken ct) => Task.FromResult($"{prefixo}-T-001");
    }

    private static EmitirOrdemServicoCommandHandler Emissor(Cenario c)
    {
        var mediator = new MediatorSimulado(req => req switch
        {
            ObterMinutaOrdemServicoQuery q => new ObterMinutaOrdemServicoQueryHandler(c.Global).Handle(q, default).GetAwaiter().GetResult(),
            ListarOrdensServicoQuery q => new ListarOrdensServicoQueryHandler(c.Global).Handle(q, default).GetAwaiter().GetResult(),
            _ => throw new NotSupportedException(),
        });
        return new EmitirOrdemServicoCommandHandler(c.Global, mediator, new NumeroFalso(), new AuditoriaFalsa());
    }

    [Fact]
    public async Task Emitir_OsAguardandoAssinatura_VoltaMesmoComPendenciaNova()
    {
        var c = await CriarAsync();
        var os = new OrdemServico { TrabalhadorId = c.Trabalhador.Id, ObraId = ObraA, Numero = "OS-1", Revisao = 0, EmitidaEm = DateTime.UtcNow, ConteudoJson = "{}" };
        c.Global.OrdensServico.Add(os);
        c.Global.DocumentosAssinatura.Add(new DocumentoAssinatura
        {
            EntidadeTipo = OrdemServico.EntidadeTipoAssinatura, EntidadeId = os.Id, ObraId = ObraA, Status = StatusDocumentoAssinatura.EmAndamento,
        });
        await c.Global.SaveChangesAsync();
        Assert.False((await Minuta(c))!.PodeEmitir);

        var r = await Emissor(c).Handle(new EmitirOrdemServicoCommand(c.Trabalhador.Id), default);

        Assert.Equal(os.Id, r.Id);
        Assert.Equal(1, await c.Global.OrdensServico.CountAsync());
    }

    [Fact]
    public async Task Emitir_SemOsAguardando_ComPendencia_NaoEmiteOsNova()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Emissor(c).Handle(new EmitirOrdemServicoCommand(c.Trabalhador.Id), default));

        Assert.Empty(await c.Global.OrdensServico.ToListAsync());
    }

    [Fact]
    public async Task Sugerir_DuasFuncoesComNomeParecido_CadaUmaRecebeSuaSugestaoPeloCodigo()
    {
        var c = await CriarAsync();
        var outra = new Funcao { Nome = "armador" };
        c.Global.Funcoes.Add(outra);
        c.Global.GheFuncoes.Add(new GheFuncao { GheId = c.Ghe.Id, FuncaoId = outra.Id });
        await c.Global.SaveChangesAsync();
        var analista = new AnalistaFalso(new SugestaoFuncaoIa("F02", new() { new("A001", "só a segunda") }));

        await new SugerirAtividadesFuncaoGheCommandHandler(c.Global, analista)
            .Handle(new SugerirAtividadesFuncaoGheCommand(ObraA, c.Ghe.Id), default);

        Assert.Equal(2, analista.Contextos.Single().Funcoes.Count);
        var gravado = await c.Global.AtividadesFuncoes.SingleAsync();
        Assert.NotEqual(Guid.Empty, gravado.FuncaoId);
        Assert.Equal(c.Global.GheFuncoes.Where(g => g.GheId == c.Ghe.Id).Count(), 2);
    }
}
